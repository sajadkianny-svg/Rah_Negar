using Rah_Negar.Qualification;

if (args.Length >= 1 && args[0].Equals("--performance", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length != 2) throw new ArgumentException("Usage: QualificationTool --performance <evidence-json>");
    Batch3PerformanceProbe.Run(args[1]);
    return;
}

if (args.Length >= 1 && args[0].Equals("--generic", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length != 3 || !int.TryParse(args[2], out int genericUnitCount))
        throw new ArgumentException("Usage: QualificationTool --generic <output-directory> <unit-count>");
    QualificationEnvironment.PrepareGeneric(args[1], genericUnitCount);
    Console.WriteLine($"Prepared generic qualification profile with {genericUnitCount} units under {Path.GetFullPath(args[1])}");
    return;
}

if (args.Length != 1) throw new ArgumentException("Usage: QualificationTool <output-directory>");
QualificationEnvironment.Prepare(args[0]);
Console.WriteLine($"Prepared qualification databases under {Path.GetFullPath(args[0])}");
