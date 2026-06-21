using System.Threading.Tasks;

namespace CrossEscPos.Wasm;

public static class Program
{
    // Nothing to run — the runtime stays loaded after Main returns and JavaScript calls the
    // [JSExport] methods in ReceiptInterop via getAssemblyExports().
    public static Task Main() => Task.CompletedTask;
}
