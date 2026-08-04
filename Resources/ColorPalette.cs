using System.Collections.Generic;
using Lada.Models;

namespace Lada.Resources;

public static class ColorPalette
{
    public static readonly IReadOnlyList<string> Midnight = new List<string>
    {
        "#5B8DEF", // Bleu accent (défaut)
        "#8B7CF6", // Violet
        "#34D399", // Vert
        "#F59E0B", // Ambre
        "#F87171", // Rouge
        "#F472B6", // Rose
        "#22D3EE", // Cyan
        "#8B92A5", // Gris
    };

    // Bauhaus/De Stijl inspired: mêmes teintes de base que Midnight, en
    // version pleine/plate au lieu de pastel, cohérent avec le fond blanc
    // uni et les bordures franches du thème Modernism.
    public static readonly IReadOnlyList<string> Modernism = new List<string>
    {
        "#1C3F94", // Bleu
        "#6E2594", // Violet
        "#1B7A3D", // Vert
        "#E8720C", // Orange
        "#D91E18", // Rouge
        "#F5C400", // Jaune
        "#111111", // Noir
        "#6E6E6E", // Gris
    };

    // Palette terminal façon ANSI, choisie pour rester lisible sur fond
    // noir (thème Anderson).
    public static readonly IReadOnlyList<string> Anderson = new List<string>
    {
        "#33FF33", // Vert (défaut)
        "#FFB000", // Ambre
        "#33FFFF", // Cyan
        "#FF3333", // Rouge
        "#FF33CC", // Magenta
        "#3388FF", // Bleu
        "#E8E8E8", // Blanc
        "#888888", // Gris
    };

    public static IReadOnlyList<string> ForTheme(AppTheme theme) => theme switch
    {
        AppTheme.Modernism => Modernism,
        AppTheme.Anderson => Anderson,
        _ => Midnight
    };
}
