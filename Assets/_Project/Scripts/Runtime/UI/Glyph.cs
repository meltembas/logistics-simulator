using UnityEngine;
using UnityEngine.UIElements;

namespace Logistics
{
    /// <summary>Vector icon drawn with Painter2D in the element's USS text color.</summary>
    [UxmlElement]
    public partial class Glyph : VisualElement
    {
        string kind = "road";

        [UxmlAttribute]
        public string Kind
        {
            get => kind;
            set { kind = value; MarkDirtyRepaint(); }
        }

        public Glyph()
        {
            AddToClassList("glyph");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(_ => MarkDirtyRepaint());
        }

        public Glyph(string kind) : this() { this.kind = kind; }

        void Draw(MeshGenerationContext ctx)
        {
            float w = contentRect.width, h = contentRect.height;
            if (w < 2f || h < 2f) return;
            float s = Mathf.Min(w, h);
            var o = new Vector2((w - s) * 0.5f, (h - s) * 0.5f);
            Vector2 P(float x, float y) => o + new Vector2(x * s, y * s);
            var p = ctx.painter2D;
            var col = resolvedStyle.color;
            p.fillColor = col;
            p.strokeColor = col;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;
            p.lineWidth = s * 0.09f;

            switch (kind)
            {
                case "pause":
                    Rect(p, P(0.22f, 0.15f), P(0.42f, 0.85f));
                    Rect(p, P(0.58f, 0.15f), P(0.78f, 0.85f));
                    break;
                case "play":
                    Poly(p, P(0.25f, 0.12f), P(0.85f, 0.5f), P(0.25f, 0.88f));
                    break;
                case "fast":
                    Poly(p, P(0.08f, 0.18f), P(0.5f, 0.5f), P(0.08f, 0.82f));
                    Poly(p, P(0.5f, 0.18f), P(0.92f, 0.5f), P(0.5f, 0.82f));
                    break;
                case "road":
                    p.BeginPath();
                    p.MoveTo(P(0.3f, 0.95f)); p.LineTo(P(0.42f, 0.05f));
                    p.MoveTo(P(0.7f, 0.95f)); p.LineTo(P(0.58f, 0.05f));
                    p.Stroke();
                    p.lineWidth = s * 0.06f;
                    p.BeginPath();
                    p.MoveTo(P(0.5f, 0.82f)); p.LineTo(P(0.5f, 0.66f));
                    p.MoveTo(P(0.5f, 0.52f)); p.LineTo(P(0.5f, 0.4f));
                    p.MoveTo(P(0.5f, 0.27f)); p.LineTo(P(0.5f, 0.18f));
                    p.Stroke();
                    break;
                case "bridge":
                    p.BeginPath();
                    p.MoveTo(P(0.04f, 0.42f)); p.LineTo(P(0.96f, 0.42f));
                    p.Stroke();
                    p.BeginPath();
                    p.MoveTo(P(0.12f, 0.8f)); p.LineTo(P(0.12f, 0.42f));
                    p.MoveTo(P(0.88f, 0.8f)); p.LineTo(P(0.88f, 0.42f));
                    p.Stroke();
                    p.BeginPath();
                    p.Arc(P(0.5f, 0.86f), s * 0.3f, Angle.Degrees(180f), Angle.Degrees(360f));
                    p.Stroke();
                    p.lineWidth = s * 0.05f;
                    p.BeginPath();
                    p.MoveTo(P(0.05f, 0.95f)); p.BezierCurveTo(P(0.2f, 0.88f), P(0.3f, 1.0f), P(0.45f, 0.94f));
                    p.MoveTo(P(0.55f, 0.95f)); p.BezierCurveTo(P(0.7f, 0.88f), P(0.8f, 1.0f), P(0.95f, 0.94f));
                    p.Stroke();
                    break;
                case "roundabout":
                    p.BeginPath();
                    p.Arc(P(0.5f, 0.5f), s * 0.3f, Angle.Degrees(0f), Angle.Degrees(300f));
                    p.Stroke();
                    Poly(p, P(0.86f, 0.26f), P(0.92f, 0.5f), P(0.68f, 0.42f));
                    p.BeginPath();
                    p.Arc(P(0.5f, 0.5f), s * 0.1f, Angle.Degrees(0f), Angle.Degrees(360f));
                    p.Fill();
                    break;
                case "highway":
                    p.BeginPath();
                    p.MoveTo(P(0.04f, 0.62f)); p.BezierCurveTo(P(0.3f, 0.12f), P(0.7f, 0.12f), P(0.96f, 0.62f));
                    p.Stroke();
                    p.lineWidth = s * 0.07f;
                    p.BeginPath();
                    p.MoveTo(P(0.35f, 0.3f)); p.LineTo(P(0.35f, 0.92f));
                    p.MoveTo(P(0.65f, 0.3f)); p.LineTo(P(0.65f, 0.92f));
                    p.Stroke();
                    break;
                case "truck":
                    Rect(p, P(0.04f, 0.22f), P(0.62f, 0.7f));
                    Poly(p, P(0.64f, 0.36f), P(0.84f, 0.36f), P(0.96f, 0.52f), P(0.96f, 0.7f), P(0.64f, 0.7f));
                    p.BeginPath(); p.Arc(P(0.24f, 0.78f), s * 0.1f, Angle.Degrees(0f), Angle.Degrees(360f)); p.Fill();
                    p.BeginPath(); p.Arc(P(0.78f, 0.78f), s * 0.1f, Angle.Degrees(0f), Angle.Degrees(360f)); p.Fill();
                    break;
                case "parcel":
                    Poly(p, P(0.5f, 0.06f), P(0.92f, 0.26f), P(0.5f, 0.46f), P(0.08f, 0.26f));
                    var c2 = col; c2.a *= 0.75f;
                    p.fillColor = c2;
                    Poly(p, P(0.08f, 0.32f), P(0.47f, 0.52f), P(0.47f, 0.95f), P(0.08f, 0.74f));
                    c2.a = col.a * 0.55f;
                    p.fillColor = c2;
                    Poly(p, P(0.92f, 0.32f), P(0.53f, 0.52f), P(0.53f, 0.95f), P(0.92f, 0.74f));
                    break;
            }
        }

        static void Rect(Painter2D p, Vector2 a, Vector2 b)
        {
            p.BeginPath();
            p.MoveTo(a); p.LineTo(new Vector2(b.x, a.y)); p.LineTo(b); p.LineTo(new Vector2(a.x, b.y));
            p.ClosePath();
            p.Fill();
        }

        static void Poly(Painter2D p, params Vector2[] pts)
        {
            p.BeginPath();
            p.MoveTo(pts[0]);
            for (int i = 1; i < pts.Length; i++) p.LineTo(pts[i]);
            p.ClosePath();
            p.Fill();
        }
    }
}
