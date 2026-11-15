using System;
using HarmonyLib;

namespace Haulnet;

/// <summary>Access to the game's internal fields that never throws, so a game update only turns one feature off.</summary>
internal static class GameAccess
{
    /// <summary>
    /// <see cref="AccessTools.FieldRefAccess{T,F}(string)"/>, or null when the game no longer has that field. Used in
    /// static initializers: a throw there would make the whole class unusable and, during <c>Plugin.Awake</c>, stop
    /// the rest of the mod from loading.
    /// </summary>
    /// <param name="field">Field name in <typeparamref name="T"/>.</param>
    /// <param name="whatIsOff">What the player loses without the field, for the log.</param>
    public static AccessTools.FieldRef<T, F>? FieldRef<T, F>(string field, string whatIsOff) where T : class
    {
        try
        {
            return AccessTools.FieldRefAccess<T, F>(field);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"{typeof(T).Name}.{field} not found (game update?); {whatIsOff}. {e.Message}");
            return null;
        }
    }
}
