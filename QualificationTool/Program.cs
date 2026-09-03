using Rah_Negar.Qualification;

if (args.Length != 1) throw new ArgumentException("Usage: QualificationTool <output-directory>");
QualificationEnvironment.Prepare(args[0]);
Console.WriteLine($"Prepared qualification databases under {Path.GetFullPath(args[0])}");
