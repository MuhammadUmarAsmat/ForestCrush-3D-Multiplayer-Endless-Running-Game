using UnityEngine;

/// <summary>Game ke saare numbers ek hi jagah se format hote hain (K / M / km).</summary>
public static class NumberFormat
{
    /// <summary>999 -> "999", 1500 -> "1.5K", 25000 -> "25K", 1200000 -> "1.2M"</summary>
    public static string Coins(int value)
    {
        if (value < 1000) return value.ToString();
        if (value < 1000000) return (value / 1000f).ToString("0.#") + "K";
        return (value / 1000000f).ToString("0.#") + "M";
    }

    /// <summary>532 -> "532 m", 1000 -> "1 km", 1250 -> "1.25 km"</summary>
    public static string Distance(float meters)
    {
        if (meters < 1000f) return Mathf.FloorToInt(meters) + " m";
        return (meters / 1000f).ToString("0.##") + " km";
    }
}