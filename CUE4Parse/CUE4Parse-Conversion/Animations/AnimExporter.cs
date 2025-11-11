using System;
using System.Collections.Generic;
using System.IO;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Objects.Core.Math;
using CUE4Parse.UE4.Writers;
using CUE4Parse.Utils;
using CUE4Parse_Conversion.ActorX;
using CUE4Parse_Conversion.Animations.PSA;
using CUE4Parse_Conversion.Animations.UEFormat;
using CUE4Parse_Conversion.Animations.XAnim;
using CUE4Parse.UE4.Assets.Exports;

namespace CUE4Parse_Conversion.Animations
{
    public class AnimExporter : ExporterBase
    {
        public readonly List<Anim> AnimSequences;

        private AnimExporter(UObject export, ExporterOptions options) : base(export, options)
        {
            AnimSequences = new List<Anim>();
        }

        private AnimExporter(ExporterOptions options, UObject export, CAnimSet animSet)
            : this(export, options)
        {
            System.Diagnostics.Debug.WriteLine($"[AnimExporter] Processing {export.Name}: AnimSet has {animSet.Sequences.Count} sequence(s)");
            
            for (var sequenceIndex = 0; sequenceIndex < animSet.Sequences.Count; sequenceIndex++)
            {
                string ext;
                byte[] data;
                
                System.Diagnostics.Debug.WriteLine($"[AnimExporter] Exporting sequence {sequenceIndex} in format {Options.AnimFormat}");
                
                switch (Options.AnimFormat)
                {
                    case EAnimFormat.ActorX:
                        ext = "psa";
                        using (var Ar = new FArchiveWriter())
                        {
                            new ActorXAnim(animSet, sequenceIndex, Options).Save(Ar);
                            data = Ar.GetBuffer();
                        }
                        break;
                    case EAnimFormat.UEFormat:
                        ext = "ueanim";
                        using (var Ar = new FArchiveWriter())
                        {
                            new UEAnim(export.Name, animSet, sequenceIndex, Options).Save(Ar);
                            data = Ar.GetBuffer();
                        }
                        break;
                    case EAnimFormat.XAnim:
                        ext = "XANIM_EXPORT";
                        using (var ms = new MemoryStream())
                        using (var sw = new StreamWriter(ms))
                        {
                            new XAnimExporter(animSet, sequenceIndex, Options, export.Name).Save(sw);
                            sw.Flush();
                            data = ms.ToArray();
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(Options.AnimFormat), Options.AnimFormat, null);
                }

                System.Diagnostics.Debug.WriteLine($"[AnimExporter] Sequence {sequenceIndex} exported, data size: {data.Length} bytes");
                
                // use GetExportSavePath()?
                AnimSequences.Add(sequenceIndex > 0
                    ? new Anim($"{PackagePath}_SEQ{sequenceIndex}.{ext}", data, Options)
                    : new Anim($"{PackagePath}.{ext}", data, Options));
            }
            
            System.Diagnostics.Debug.WriteLine($"[AnimExporter] Total AnimSequences added: {AnimSequences.Count}");
        }

        private AnimExporter(ExporterOptions options, USkeleton skeleton, UAnimSequence? animSequence = null)
            : this(options, animSequence != null ? animSequence : skeleton, skeleton.ConvertAnims(animSequence) ?? throw new InvalidOperationException("Failed to convert animation - this animation may use an unsupported ACL compression format."))
        {

        }

        private AnimExporter(ExporterOptions options, USkeleton skeleton, UAnimMontage? animMontage = null)
            : this(options, animMontage != null ? animMontage : skeleton, skeleton.ConvertAnims(animMontage) ?? throw new InvalidOperationException("Failed to convert animation - this animation may use an unsupported ACL compression format."))
        {

        }

        private AnimExporter(ExporterOptions options, USkeleton skeleton, UAnimComposite? animComposite = null)
            : this(options, animComposite != null ? animComposite : skeleton, skeleton.ConvertAnims(animComposite) ?? throw new InvalidOperationException("Failed to convert animation - this animation may use an unsupported ACL compression format."))
        {

        }

        public AnimExporter(UAnimSequence animSequence, ExporterOptions options) : this(options, animSequence.Skeleton.Load<USkeleton>()!, animSequence) { }
        public AnimExporter(UAnimMontage animMontage, ExporterOptions options) : this(options, animMontage.Skeleton.Load<USkeleton>()!, animMontage) { }
        public AnimExporter(UAnimComposite animComposite, ExporterOptions options) : this(options, animComposite.Skeleton.Load<USkeleton>()!, animComposite) { }
        

        public override bool TryWriteToDir(DirectoryInfo baseDirectory, out string label, out string savedFilePath)
        {
            var b = false;
            label = string.Empty;
            savedFilePath = PackagePath;
            if (AnimSequences.Count == 0) return b;

            var outText = "SEQ ";
            for (var i = 0; i < AnimSequences.Count; i++)
            {
                b |= AnimSequences[i].TryWriteToDir(baseDirectory, out label, out savedFilePath);
                outText += $"{i} ";
            }

            label = outText + $"as '{savedFilePath.SubstringAfterWithLast('.')}' for '{ExportName}'";
            return b;
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
