using System;
using System.Collections.Generic;
using TMPro;
using TownOfHost.Modules.MainMenu;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TownOfHost;

// Procedural water geometry, never a stretched screenshot or a delayed rectangular cover.
internal sealed class SeasideWaveView : IDisposable
{
    readonly GameObject root;
    readonly Mesh mesh;
    readonly Material material;
    readonly TextMeshPro[] letters = new TextMeshPro[SeasideTransition.Word.Length];
    readonly List<Vector3> vertices = new();
    readonly List<Color> colors = new();
    readonly List<int> triangles = new();
    bool disposed;
    double width, height;
    internal bool Visible => root != null && root.activeSelf;
    internal SeasideWaveView(Transform parent, TextMeshPro font)
    {
        root = new GameObject("SeasideWaveTransition"); root.layer = parent.gameObject.layer;
        root.transform.SetParent(parent, false); root.transform.localPosition = new(0, 0, -.4f);
        root.SetActive(false);
        try
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) throw new InvalidOperationException("Menu shader unavailable");
            mesh = new Mesh(); mesh.MarkDynamic(); root.AddComponent<MeshFilter>().mesh = mesh;
            material = new Material(shader);
            var renderer = root.AddComponent<MeshRenderer>(); renderer.material = material; renderer.sortingOrder = 500;
            for (int i = 0; i < letters.Length; i++)
            {
                var text = Object.Instantiate(font, root.transform); text.name = "WaveLetter_" + i; text.DestroyTranslator();
                var aspect = text.GetComponent<AspectPosition>();
                if (aspect != null) { aspect.enabled = false; Object.Destroy(aspect); }
                text.text = SeasideTransition.Word[i].ToString(); text.fontStyle = FontStyles.Italic;
                text.alignment = TextAlignmentOptions.Center; text.enableWordWrapping = false; text.enableAutoSizing = false;
                text.fontSize = 13.6f; text.rectTransform.sizeDelta = new(1.6f, 1.8f); text.transform.localScale = Vector3.one;
                text.gameObject.SetActive(true); text.GetComponent<MeshRenderer>().enabled = true;
                text.GetComponent<MeshRenderer>().sortingOrder = 501; letters[i] = text;
            }
        }
        catch { Dispose(); throw; }
    }
    void Quad(double ax, double ay, double bx, double by, double cx, double cy, double dx, double dy, Color a, Color b, Color c, Color d)
    {
        int i = vertices.Count;
        vertices.Add(Point(ax, ay)); vertices.Add(Point(bx, by)); vertices.Add(Point(cx, cy)); vertices.Add(Point(dx, dy));
        colors.Add(a); colors.Add(b); colors.Add(c); colors.Add(d);
        triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 1); triangles.Add(i); triangles.Add(i + 3); triangles.Add(i + 2);
    }
    Vector3 Point(double x, double y) => new((float)((x - .5) * width), (float)((.5 - y) * height), 0);
    static Color Water(double x, double y)
    {
        float t = (float)SeasideTransition.Clamp(x * .75 + y * .20);
        // Match the reference: deep blue left, luminous turquoise right. Opaque core hides the page swap.
        return new(.10f + .28f * t, .29f + .49f * t, .51f + .32f * t, 1);
    }
    void Streamlet(SeasideTransition state, double x0, double y0, double x1, double y1, double thickness, Color color)
    {
        var a = state.Span(y0); var b = state.Span(y1);
        x0 = Math.Max(a.Left, Math.Min(a.Right, x0)); x1 = Math.Max(b.Left, Math.Min(b.Right, x1));
        if (x0 >= 1 || x1 <= 0 || x1 - x0 < .0001) return;
        x0 = Math.Max(0, x0); x1 = Math.Min(1, x1);
        Quad(x0, y0, x1, y1, x1, y1 + thickness, x0, y0 + thickness, color, color, color, color);
    }
    internal void Draw(SeasideTransition state, double viewportWidth, double viewportHeight)
    {
        if (!state.Active) { Hide(); return; }
        width = viewportWidth; height = viewportHeight; root.SetActive(true);
        vertices.Clear(); colors.Clear(); triangles.Clear();
        const int rows = 96, columns = 10;
        for (int row = 0; row < rows; row++)
        {
            double y0 = (double)row / rows, y1 = (double)(row + 1) / rows;
            var a = state.Span(y0); var b = state.Span(y1);
            double al = Math.Max(0, a.Left), ar = Math.Min(1, a.Right), bl = Math.Max(0, b.Left), br = Math.Min(1, b.Right);
            if (ar <= al && br <= bl) continue;
            ar = Math.Max(al, ar); br = Math.Max(bl, br);
            for (int col = 0; col < columns; col++)
            {
                double u = (double)col / columns, v = (double)(col + 1) / columns;
                double x0 = al + (ar - al) * u, x1 = al + (ar - al) * v, x2 = bl + (br - bl) * v, x3 = bl + (br - bl) * u;
                Quad(x0,y0,x1,y0,x2,y1,x3,y1,Water(x0,y0),Water(x1,y0),Water(x2,y1),Water(x3,y1));
            }
            bool leading = state.Time < SeasideTransition.EnterEnd;
            bool edging = leading || state.Time > SeasideTransition.HoldEnd;
            if (edging)
            {
                bool right = leading != state.Reverse;
                double x0 = right ? a.Right : a.Left, x1 = right ? b.Right : b.Left;
                double sign = right ? -1 : 1;
                if (x0 > -.08 && x0 < 1.08)
                {
                    var clear = new Color(.75f, 1, 1, 0); var foam = new Color(.8f, 1, 1, .7f);
                    Quad(x0,y0,x0+sign*.026,y0,x1+sign*.026,y1,x1,y1,foam,clear,clear,foam);
                }
            }
        }
        // Fine diagonal streamlines and quiet glints echo the video without additional textures.
        for (int line = -3; line < 20; line++)
            for (int section = 0; section < 24; section++)
            {
                double x = section / 24d, next = (section + 1) / 24d;
                double y = line * .065 - x * .33 + state.Time * .012;
                double yn = line * .065 - next * .33 + state.Time * .012;
                if (y > 0 && y < .99 && yn > 0 && yn < .99)
                    Streamlet(state,x,y,next,yn,.004,new(.65f,.94f,1,.17f));
            }
        for (int i = 0; i < 9; i++)
        {
            double x = .12 + (i * .193 % .78), y = .12 + (i * .271 % .75);
            var span = state.Span(y);
            if (x > span.Left + .01 && x < span.Right - .01)
            {
                float alpha = (float)(.3 + .15 * Math.Sin(state.Time * 2 + i));
                var c = new Color(.82f,1,1,alpha);
                Quad(x-.002,y-.003,x+.002,y-.003,x+.002,y+.003,x-.002,y+.003,c,c,c,c);
            }
        }
        mesh.Clear(); mesh.vertices = vertices.ToArray(); mesh.colors = colors.ToArray(); mesh.triangles = triangles.ToArray(); mesh.RecalculateBounds();
        for (int i = 0; i < letters.Length; i++)
        {
            var pose = state.Letter(i); var text = letters[i];
            text.transform.localPosition = new((float)((pose.X-.5)*width),(float)((.5-pose.Y)*height),-.02f);
            text.transform.localRotation = Quaternion.Euler(0,0,(float)pose.Rotation);
            text.color = new(1,1,.96f,(float)pose.Alpha);
        }
    }
    internal void Hide() { if (root != null) root.SetActive(false); }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        if (root != null) Object.Destroy(root);
        if (mesh != null) Object.Destroy(mesh); if (material != null) Object.Destroy(material);
    }
}
