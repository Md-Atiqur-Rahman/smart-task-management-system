using System.Runtime.CompilerServices;

// NSubstitute (Castle DynamicProxy) must see internal test types to substitute
// interfaces that use them as generic arguments.
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
