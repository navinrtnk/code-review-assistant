namespace SampleProject;

public sealed class Calculator : ICalculator
{
    public int Add(int left, int right)
    {
        var total = left + right;
        return total;
    }
}
