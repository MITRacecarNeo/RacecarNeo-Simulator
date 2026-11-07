using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// A label on a parallelogram fill leaning right by ThemeTokens.BadgeSkewDegrees, as in the
/// lap-timer and badge motif. The text stays upright and inherits font and color from this
/// element's style. The fill color comes from the --fill style property.
/// </summary>
[UxmlElement]
public partial class ParallelogramElement : VisualElement
{
    private static readonly CustomStyleProperty<Color> fillProperty = new CustomStyleProperty<Color>("--fill");

    private readonly Label label = new Label();
    private Color fill = Color.clear;

    public ParallelogramElement()
    {
        this.label.pickingMode = PickingMode.Ignore;
        this.label.style.paddingLeft = 0;
        this.label.style.paddingRight = 0;
        this.label.style.paddingTop = 0;
        this.label.style.paddingBottom = 0;
        this.label.style.marginLeft = 0;
        this.label.style.marginRight = 0;
        this.label.style.marginTop = 0;
        this.label.style.marginBottom = 0;
        this.Add(this.label);
        this.generateVisualContent += this.Draw;
        this.RegisterCallback<CustomStyleResolvedEvent>(this.OnStyleResolved);
    }

    /// <summary>
    /// The text drawn on the badge.
    /// </summary>
    [UxmlAttribute]
    public string Text
    {
        get => this.label.text;
        set => this.label.text = value;
    }

    /// <summary>
    /// Horizontal distance between the top and bottom edges for a badge of the given height.
    /// </summary>
    public static float Lean(float height)
    {
        return height * Mathf.Tan(ThemeTokens.BadgeSkewDegrees * Mathf.Deg2Rad);
    }

    private void OnStyleResolved(CustomStyleResolvedEvent evt)
    {
        if (evt.customStyle.TryGetValue(ParallelogramElement.fillProperty, out Color color))
        {
            this.fill = color;
            this.MarkDirtyRepaint();
        }
    }

    private void Draw(MeshGenerationContext context)
    {
        float width = this.layout.width;
        float height = this.layout.height;
        if (width <= 0 || height <= 0 || this.fill.a <= 0)
        {
            return;
        }

        float lean = ParallelogramElement.Lean(height);
        Painter2D painter = context.painter2D;
        painter.fillColor = this.fill;
        painter.BeginPath();
        painter.MoveTo(new Vector2(lean, 0));
        painter.LineTo(new Vector2(width, 0));
        painter.LineTo(new Vector2(width - lean, height));
        painter.LineTo(new Vector2(0, height));
        painter.ClosePath();
        painter.Fill();
    }
}
