namespace T48Sdk;

public sealed record T48Progress(
    string Operation,
    long Completed,
    long Total,
    string? Message = null)
{
    public double Percent => Total <= 0 ? 0 : Math.Clamp((double)Completed / Total * 100, 0, 100);
}
