using SampleSolution.Domain;

namespace SampleSolution.App;

public sealed class GreetingService
{
    public Greeting Create() => new("Hello from the solution fixture");
}
