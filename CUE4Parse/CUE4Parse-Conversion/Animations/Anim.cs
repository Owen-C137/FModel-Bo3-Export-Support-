using System;
using System.Diagnostics;
using System.IO;

namespace CUE4Parse_Conversion.Animations
{
    public class Anim : ExporterBase
    {
        public readonly string FileName;
        public readonly byte[] FileData;
        public new readonly ExporterOptions? Options;

        public Anim(string fileName, byte[] fileData, ExporterOptions? options = null)
        {
            FileName = fileName;
            FileData = fileData;
            Options = options;
        }

        public override bool TryWriteToDir(DirectoryInfo baseDirectory, out string label, out string savedFilePath)
        {
            label = string.Empty;
            savedFilePath = string.Empty;
            if (FileData.Length <= 0) return false;

            savedFilePath = FixAndCreatePath(baseDirectory, FileName);
            File.WriteAllBytes(savedFilePath, FileData);
            label = Path.GetFileName(savedFilePath);
            
            // Auto-convert XANIM_EXPORT to xanim_bin using ExportX
            if (savedFilePath.EndsWith(".XANIM_EXPORT", StringComparison.OrdinalIgnoreCase))
            {
                var convertedPath = TryConvertToXAnimBin(savedFilePath);
                if (convertedPath != null)
                {
                    savedFilePath = convertedPath;
                    label = Path.GetFileName(savedFilePath);
                }
            }
            
            return File.Exists(savedFilePath);
        }

        private string? TryConvertToXAnimBin(string xanimExportPath)
        {
            try
            {
                // Normalize path separators to avoid issues with mixed slashes
                xanimExportPath = xanimExportPath.Replace('/', '\\');
                
                string? converterPath = null;
                
                // First, try to use the Export2BinPath from options if provided
                if (Options.HasValue && !string.IsNullOrEmpty(Options.Value.Export2BinPath) && File.Exists(Options.Value.Export2BinPath))
                {
                    converterPath = Options.Value.Export2BinPath;
                }
                else
                {
                    // Fall back to looking for exportx.exe
                    var outputDir = Path.GetDirectoryName(xanimExportPath);
                    if (string.IsNullOrEmpty(outputDir))
                    {
                        return null;
                    }

                    // Go up to find the Output root directory, then go up one more to FModel directory
                    var currentDir = new DirectoryInfo(outputDir);
                    while (currentDir != null && currentDir.Name != "Output")
                    {
                        currentDir = currentDir.Parent;
                    }

                    if (currentDir != null)
                    {
                        // Go up one more level to the FModel directory (parent of Output)
                        var fmodelDir = currentDir.Parent;
                        if (fmodelDir != null)
                        {
                            var exportXPath = Path.Combine(fmodelDir.FullName, "exportx.exe");
                            if (File.Exists(exportXPath))
                            {
                                converterPath = exportXPath;
                            }
                        }
                    }
                }

                if (converterPath != null)
                {
                    Console.WriteLine($"[DEBUG] Converting XANIM: {xanimExportPath}");
                    Console.WriteLine($"[DEBUG] Using converter: {converterPath}");
                    
                    var workingDir = Path.GetDirectoryName(xanimExportPath);
                    var fileName = Path.GetFileName(xanimExportPath);
                    
                    Console.WriteLine($"[DEBUG] Working directory: {workingDir}");
                    Console.WriteLine($"[DEBUG] File name: {fileName}");
                    
                    // Run converter to convert the file
                    // Use just the filename as argument since working directory is set
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = converterPath,
                            Arguments = $"\"{fileName}\"",
                            WorkingDirectory = workingDir,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        }
                    };

                    process.Start();
                    
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    
                    process.WaitForExit(5000); // 5 second timeout

                    Console.WriteLine($"[DEBUG] Converter exit code: {process.ExitCode}");
                    if (!string.IsNullOrEmpty(output))
                        Console.WriteLine($"[DEBUG] Output: {output}");
                    if (!string.IsNullOrEmpty(error))
                        Console.WriteLine($"[DEBUG] Error: {error}");

                    if (process.ExitCode == 0)
                    {
                        // Check if the .xanim_bin was created
                        var xanimBinPath = Path.ChangeExtension(xanimExportPath, ".xanim_bin");
                        
                        Console.WriteLine($"[DEBUG] Checking for output file: {xanimBinPath}");
                        
                        if (File.Exists(xanimBinPath))
                        {
                            // Delete the original .XANIM_EXPORT file
                            File.Delete(xanimExportPath);
                            Console.WriteLine($"Animation conversion completed for {Path.GetFileName(xanimExportPath)} - original file deleted");
                            return xanimBinPath;
                        }
                        else
                        {
                            Console.WriteLine($"[DEBUG] Output file not found!");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to convert XANIM_EXPORT: {ex.Message}");
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
