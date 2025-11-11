using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CUE4Parse_Conversion.Materials;

namespace CUE4Parse_Conversion.Meshes
{
    public class Mesh : ExporterBase
    {
        public readonly string FileName;
        public readonly byte[] FileData;
        public readonly List<MaterialExporter2> Materials;
        public Dictionary<string, Action<string>>? AdditionalFiles;
        public new readonly ExporterOptions? Options;

        public Mesh(string fileName, byte[] fileData, List<MaterialExporter2> materials, ExporterOptions? options = null)
        {
            FileName = fileName;
            FileData = fileData;
            Materials = materials;
            Options = options;
        }

        private readonly object _material = new ();
        public override bool TryWriteToDir(DirectoryInfo baseDirectory, out string label, out string savedFilePath)
        {
            label = string.Empty;
            savedFilePath = string.Empty;
            if (FileData.Length <= 0) return false;

            Parallel.ForEach(Materials, material =>
            {
                lock (_material) material.TryWriteToDir(baseDirectory, out _, out _);
            });

            savedFilePath = FixAndCreatePath(baseDirectory, FileName);
            File.WriteAllBytes(savedFilePath, FileData);
            label = Path.GetFileName(savedFilePath);
            
            // Export additional files (like material JSON)
            if (AdditionalFiles != null)
            {
                foreach (var (path, action) in AdditionalFiles)
                {
                    try
                    {
                        action(savedFilePath);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to export additional file {path}: {ex.Message}");
                    }
                }
            }
            
            // Auto-convert XModel files to binary using ExportX if available
            if (savedFilePath.EndsWith(".XMODEL_EXPORT", StringComparison.OrdinalIgnoreCase))
            {
                var convertedPath = TryConvertToXModelBin(savedFilePath);
                if (convertedPath != null)
                {
                    savedFilePath = convertedPath;
                    label = Path.GetFileName(savedFilePath);
                }
            }
            
            return File.Exists(savedFilePath);
        }
        
        private string? TryConvertToXModelBin(string xmodelExportPath)
        {
            try
            {
                // Normalize path separators to avoid issues with mixed slashes
                xmodelExportPath = xmodelExportPath.Replace('/', '\\');
                
                string? converterPath = null;
                
                // First, try to use the Export2BinPath from options if provided
                if (Options.HasValue && !string.IsNullOrEmpty(Options.Value.Export2BinPath) && File.Exists(Options.Value.Export2BinPath))
                {
                    converterPath = Options.Value.Export2BinPath;
                }
                else
                {
                    // Fall back to looking for exportx.exe in the project root (go up from output directory)
                    var directory = new DirectoryInfo(xmodelExportPath);
                    DirectoryInfo? current = directory.Parent;
                    
                    // Search up the directory tree for exportx.exe
                    while (current != null && converterPath == null)
                    {
                        var potentialPath = Path.Combine(current.FullName, "exportx.exe");
                        if (File.Exists(potentialPath))
                        {
                            converterPath = potentialPath;
                            break;
                        }
                        current = current.Parent;
                    }
                    
                    if (converterPath == null)
                    {
                        // Also try common locations
                        var possiblePaths = new[]
                        {
                            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exportx.exe"),
                            Path.Combine(Environment.CurrentDirectory, "exportx.exe")
                        };
                        
                        foreach (var path in possiblePaths)
                        {
                            if (File.Exists(path))
                            {
                                converterPath = path;
                                break;
                            }
                        }
                    }
                }
                
                if (converterPath != null)
                {
                    Console.WriteLine($"[DEBUG] Converting XMODEL: {xmodelExportPath}");
                    Console.WriteLine($"[DEBUG] Using converter: {converterPath}");
                    
                    var workingDir = Path.GetDirectoryName(xmodelExportPath);
                    var fileName = Path.GetFileName(xmodelExportPath);
                    
                    Console.WriteLine($"[DEBUG] Working directory: {workingDir}");
                    Console.WriteLine($"[DEBUG] File name: {fileName}");
                    
                    // Run converter to convert to binary
                    // Use just the filename as argument since working directory is set
                    var processInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = converterPath,
                        Arguments = $"\"{fileName}\"",
                        WorkingDirectory = workingDir,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    
                    using var process = System.Diagnostics.Process.Start(processInfo);
                    if (process != null)
                    {
                        var output = process.StandardOutput.ReadToEnd();
                        var error = process.StandardError.ReadToEnd();
                        
                        process.WaitForExit(5000); // 5 second timeout
                        
                        Console.WriteLine($"[DEBUG] Converter exit code: {process.ExitCode}");
                        if (!string.IsNullOrEmpty(output))
                            Console.WriteLine($"[DEBUG] Output: {output}");
                        if (!string.IsNullOrEmpty(error))
                            Console.WriteLine($"[DEBUG] Error: {error}");
                        
                        // Check if conversion was successful by looking for the output file
                        var binPath = Path.ChangeExtension(xmodelExportPath, ".xmodel_bin");
                        Console.WriteLine($"[DEBUG] Checking for output file: {binPath}");
                        
                        if (File.Exists(binPath))
                        {
                            // Delete the original .XMODEL_EXPORT file as it's no longer needed
                            try
                            {
                                File.Delete(xmodelExportPath);
                                Console.WriteLine($"Model conversion completed for {Path.GetFileName(xmodelExportPath)} - original file deleted");
                                return binPath;
                            }
                            catch
                            {
                                Console.WriteLine($"Model conversion completed for {Path.GetFileName(xmodelExportPath)} - could not delete original file");
                                return binPath;
                            }
                        }
                        else
                        {
                            Console.WriteLine($"[DEBUG] Output file not found!");
                            Console.WriteLine($"Model conversion completed for {Path.GetFileName(xmodelExportPath)}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to auto-convert XModel to binary: {ex.Message}");
            }
            
            return null;
        }

        public override bool TryWriteToZip(out byte[] zipFile)
        {
            throw new NotImplementedException();
        }

        public override void AppendToZip()
        {
            throw new NotImplementedException();
        }
    }
}
