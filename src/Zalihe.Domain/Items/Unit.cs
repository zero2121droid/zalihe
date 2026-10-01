namespace Zalihe.Domain.Items;

/// <summary>
/// Unit of measure. A fixed list, so reports can add up quantities
/// (free text would end up as "kom", "Kom." and "komad").
/// Serialized as lowercase codes ("kom", "kg"); the frontend translates them.
/// </summary>
public enum Unit
{
    Kom,
    Kg,
    G,
    L,
    Ml,
    M,
    Pak,
}
