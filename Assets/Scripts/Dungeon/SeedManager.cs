using UnityEngine;

/// <summary>
/// Manages dungeon seed generation and provides a deterministic Random instance.
/// Use this to ensure the same seed always produces the same dungeon layout,
/// which is useful for debugging, replay, or shareable level codes.
/// </summary>
public static class SeedManager
{
    /// <summary>Current active seed.</summary>
    public static int CurrentSeed { get; private set; }

    /// <summary>Deterministic random number generator tied to the current seed.</summary>
    public static System.Random Rng { get; private set; }

    private static bool _initialized;

    /// <summary>Initialize with a new random seed. Call once before dungeon generation.</summary>
    public static void Initialize()
    {
        CurrentSeed = GenerateRandomSeed();
        Rng = new System.Random(CurrentSeed);
        _initialized = true;
        Debug.Log($"[SeedManager] Initialized with seed: {CurrentSeed}");
    }

    /// <summary>Generate a new random seed and re-initialize.</summary>
    public static void Regenerate()
    {
        CurrentSeed = GenerateRandomSeed();
        Rng = new System.Random(CurrentSeed);
        Debug.Log($"[SeedManager] Regenerated seed: {CurrentSeed}");
    }

    /// <summary>Set a specific seed value (e.g. from user input or shareable level code).</summary>
    public static void SetSeed(int seed)
    {
        CurrentSeed = seed;
        Rng = new System.Random(seed);
        Debug.Log($"[SeedManager] Seed set to: {seed}");
    }

    /// <summary>Get a human-readable string representation of the current seed.</summary>
    public static string GetSeedString() => CurrentSeed.ToString();

    /// <summary>Check whether Initialize/Regenerate/SetSeed has been called yet.</summary>
    public static bool IsInitialized => _initialized;

    /// <summary>Inclusive-range integer next.</summary>
    public static int Range(System.Random rng, int inclusiveMin, int inclusiveMax) =>
        rng.Next(inclusiveMin, inclusiveMax + 1);

    /// <summary>Float next in [minExclusive, maxExclusive).</summary>
    public static float NextFloat(System.Random rng, float minExclusive, float maxExclusive) =>
        (float)rng.NextDouble() * (maxExclusive - minExclusive) + minExclusive;

    private static int GenerateRandomSeed()
    {
        // Cross-platform deterministic entropy — DateTime.Now.GetHashCode() is unreliable across
        // different .NET runtimes / process restarts, so we use a cryptographically strong source.
        try
        {
            var cryptoBytes = new byte[4];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(cryptoBytes);
            }
            int cryptoSeed = cryptoBytes[0] | (cryptoBytes[1] << 8) | (cryptoBytes[2] << 16) | (cryptoBytes[3] << 24);
            return cryptoSeed ^ Random.Range(int.MinValue, int.MaxValue);
        }
        catch
        {
            // Fallback if crypto RNG is unavailable (unlikely but defensive).
            return System.DateTime.Now.Ticks.GetHashCode() ^ Random.Range(int.MinValue, int.MaxValue);
        }
    }
}
