using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// A flat line icon drawn as vector strokes in the element's text color: rounded caps and joins,
/// no fills except the play triangle and the race flag's checks, on a 24-unit grid scaled to the
/// element's size.
/// </summary>
[UxmlElement]
public partial class MenuIcon : VisualElement
{
    /// <summary>
    /// The icons in the set.
    /// </summary>
    public enum Glyph
    {
        Play,
        Settings,
        BestTimes,
        Controls,
        Exit,
        Back,
        Exploration,
        Autograder,
        Race,
        Link,
        Plus,
    }

    /// <summary>
    /// Stroke width in grid units (24 units across).
    /// </summary>
    private const float strokeUnits = 2.25f;

    private Glyph glyph;

    public MenuIcon()
    {
        this.pickingMode = PickingMode.Ignore;
        this.generateVisualContent += this.Draw;
        this.RegisterCallback<CustomStyleResolvedEvent>(_ => this.MarkDirtyRepaint());
    }

    /// <summary>
    /// The icon to draw.
    /// </summary>
    [UxmlAttribute]
    public Glyph Icon
    {
        get => this.glyph;
        set
        {
            this.glyph = value;
            this.MarkDirtyRepaint();
        }
    }

    private void Draw(MeshGenerationContext context)
    {
        float size = Mathf.Min(this.layout.width, this.layout.height);
        if (size <= 0)
        {
            return;
        }

        Painter2D p = context.painter2D;
        Vector2 origin = new Vector2((this.layout.width - size) / 2, (this.layout.height - size) / 2);
        float unit = size / 24;
        Vector2 P(float x, float y) => origin + new Vector2(x, y) * unit;

        Color color = this.resolvedStyle.color;
        p.strokeColor = color;
        p.fillColor = color;
        p.lineWidth = MenuIcon.strokeUnits * unit;
        p.lineCap = LineCap.Round;
        p.lineJoin = LineJoin.Round;

        switch (this.glyph)
        {
            case Glyph.Play:
                p.BeginPath();
                p.MoveTo(P(8, 5));
                p.LineTo(P(19, 12));
                p.LineTo(P(8, 19));
                p.ClosePath();
                p.Fill();
                p.Stroke();
                break;

            case Glyph.Settings:
                MenuIcon.Circle(p, P(12, 12), 3.5f * unit);
                MenuIcon.Circle(p, P(12, 12), 7 * unit);
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI / 4;
                    Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    MenuIcon.Line(p, P(12, 12) + direction * 7 * unit, P(12, 12) + direction * 10 * unit);
                }
                break;

            case Glyph.BestTimes:
                MenuIcon.Circle(p, P(12, 13.5f), 7.5f * unit);
                MenuIcon.Line(p, P(10, 3), P(14, 3));
                MenuIcon.Line(p, P(12, 3), P(12, 6));
                MenuIcon.Line(p, P(12, 13.5f), P(12, 9.5f));
                MenuIcon.Line(p, P(12, 13.5f), P(15, 15.5f));
                break;

            case Glyph.Controls:
                p.BeginPath();
                p.MoveTo(P(7, 7));
                p.LineTo(P(17, 7));
                p.ArcTo(P(22, 7), P(22, 16), 4 * unit);
                p.LineTo(P(22, 16));
                p.ArcTo(P(22, 19.5f), P(18, 18), 2.5f * unit);
                p.LineTo(P(15.5f, 15.5f));
                p.LineTo(P(8.5f, 15.5f));
                p.LineTo(P(6, 18));
                p.ArcTo(P(2, 19.5f), P(2, 16), 2.5f * unit);
                p.LineTo(P(2, 11));
                p.ArcTo(P(2, 7), P(7, 7), 4 * unit);
                p.ClosePath();
                p.Stroke();
                MenuIcon.Line(p, P(7.5f, 9.5f), P(7.5f, 13));
                MenuIcon.Line(p, P(5.75f, 11.25f), P(9.25f, 11.25f));
                MenuIcon.Dot(p, P(15.5f, 10), 1.1f * unit);
                MenuIcon.Dot(p, P(17.5f, 12.5f), 1.1f * unit);
                break;

            case Glyph.Exit:
                p.BeginPath();
                p.Arc(P(12, 13), 7.5f * unit, Angle.Degrees(-55), Angle.Degrees(235));
                p.Stroke();
                MenuIcon.Line(p, P(12, 3.5f), P(12, 12));
                break;

            case Glyph.Plus:
                MenuIcon.Line(p, P(12, 5), P(12, 19));
                MenuIcon.Line(p, P(5, 12), P(19, 12));
                break;

            case Glyph.Back:
                p.BeginPath();
                p.MoveTo(P(15, 5));
                p.LineTo(P(8, 12));
                p.LineTo(P(15, 19));
                p.Stroke();
                break;

            case Glyph.Exploration:
                MenuIcon.Circle(p, P(12, 12), 9 * unit);
                p.BeginPath();
                p.MoveTo(P(15.5f, 8.5f));
                p.LineTo(P(13.25f, 13.25f));
                p.LineTo(P(8.5f, 15.5f));
                p.LineTo(P(10.75f, 10.75f));
                p.ClosePath();
                p.Fill();
                p.Stroke();
                break;

            case Glyph.Autograder:
                MenuIcon.Circle(p, P(12, 12), 9 * unit);
                p.BeginPath();
                p.MoveTo(P(7.5f, 12.5f));
                p.LineTo(P(10.5f, 15.5f));
                p.LineTo(P(16.5f, 9));
                p.Stroke();
                break;

            case Glyph.Race:
                MenuIcon.Line(p, P(5, 3.5f), P(5, 21));
                p.BeginPath();
                p.MoveTo(P(5, 4.5f));
                p.LineTo(P(19, 4.5f));
                p.LineTo(P(19, 13.5f));
                p.LineTo(P(5, 13.5f));
                p.Stroke();
                for (int row = 0; row < 2; row++)
                {
                    for (int column = 0; column < 4; column++)
                    {
                        if ((row + column) % 2 == 0)
                        {
                            float x = 5 + column * 3.5f;
                            float y = 4.5f + row * 4.5f;
                            p.BeginPath();
                            p.MoveTo(P(x, y));
                            p.LineTo(P(x + 3.5f, y));
                            p.LineTo(P(x + 3.5f, y + 4.5f));
                            p.LineTo(P(x, y + 4.5f));
                            p.ClosePath();
                            p.Fill();
                        }
                    }
                }
                break;

            case Glyph.Link:
                // Signal arcs over a dot: a connection to the car
                MenuIcon.Dot(p, P(12, 18), 1.6f * unit);
                foreach (float radius in new[] { 5.5f, 10f })
                {
                    p.BeginPath();
                    p.Arc(P(12, 18), radius * unit, Angle.Degrees(-135), Angle.Degrees(-45));
                    p.Stroke();
                }
                break;
        }
    }

    private static void Line(Painter2D p, Vector2 from, Vector2 to)
    {
        p.BeginPath();
        p.MoveTo(from);
        p.LineTo(to);
        p.Stroke();
    }

    private static void Circle(Painter2D p, Vector2 center, float radius)
    {
        p.BeginPath();
        p.Arc(center, radius, Angle.Degrees(0), Angle.Degrees(360));
        p.Stroke();
    }

    private static void Dot(Painter2D p, Vector2 center, float radius)
    {
        p.BeginPath();
        p.Arc(center, radius, Angle.Degrees(0), Angle.Degrees(360));
        p.Fill();
    }
}
