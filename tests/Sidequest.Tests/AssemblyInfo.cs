using Xunit;

// The stores expose their root directory as a mutable static, which TempStore redirects
// for the duration of a test. That is process-wide state, so running tests in parallel
// lets one test repoint the store out from under another - which is exactly what happened
// the first time this suite ran.
//
// Serialising the suite is the honest fix for now. The alternative is making the stores
// instance-based, which is the better design but touches every call site in the app; if
// this file ever becomes a bottleneck, that is the change to make.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
