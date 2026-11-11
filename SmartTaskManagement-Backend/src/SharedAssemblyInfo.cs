// Linked into every woven (src) project by Directory.Build.targets.
// Conservative NullGuard flags: arguments and return values only.
// Properties and NonPublic members are NOT checked, because EF Core
// materializes entities through private constructors and setters.
using NullGuard;

[assembly: NullGuard(ValidationFlags.Arguments | ValidationFlags.ReturnValues)]
