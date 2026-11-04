using CsTools.Extensions;

using static System.Console;
using static CsTools.Functional.Memoization;

class Certificate
{
    public static string EncryptDirectory
    {
        get => field ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "letsencrypt-cert");
    }

    public static Func<string> GetPfxPassword { get; } = Memoize(InitGetPfxPassword);

    static string InitGetPfxPassword()
        => "/etc"
                .AppendPath("letsencrypt-cert")
                ?.ReadAllTextFromFilePath()
                ?.Trim()
                ?? "".SideEffect(_ => WriteLine("!!!NO PASSWORD!!"));
}