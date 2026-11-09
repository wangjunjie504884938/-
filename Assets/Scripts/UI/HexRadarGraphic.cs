using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 六维雷达图 — 代码动态生成 UI Mesh (VertexHelper), 不用静态图片拼凑 (图片无法随数值变化)
/// 六轴顺序与标签一致: 功(顶) 防(右上) 体(右下) 智(底) 精(左下) 耐(左上)
/// gridMode=true 画背景网格(3圈+辐条); false 画数据多边形(半透明填充+描边)
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class HexRadarGraphic : MaskableGraphic
{
    public bool gridMode;
    private readonly float[] _values = { 1f, 1f, 1f, 1f, 1f, 1f };   // 0..1 归一化
    private const float LineWidth = 2f;

    /// <summary>设置六轴数值并重绘 (0..1, 功/防/体/智/精/耐 顺序)</summary>
    public void SetValues(float[] normalized)
    {
        if (normalized == null || normalized.Length != 6) return;
        for (int i = 0; i < 6; i++) _values[i] = Mathf.Clamp01(normalized[i]);
        SetVerticesDirty();
    }

    /// <summary>第i轴方向: 90°起每60°一根 (功顶→防右上→体右下→智底→精左下→耐左上)</summary>
    private static Vector2 AxisDir(int i)
    {
        float angle = (90f - 60f * i) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        float r = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * 0.46f;
        var c = Vector2.zero;

        if (gridMode)
        {
            // 3圈六边形 (r, 2r/3, r/3) + 6根辐条
            for (int ring = 3; ring >= 1; ring--)
            {
                float rr = r * ring / 3f;
                for (int i = 0; i < 6; i++)
                {
                    var a = c + AxisDir(i) * rr;
                    var b = c + AxisDir((i + 1) % 6) * rr;
                    AddLine(vh, a, b, color);
                }
            }
            for (int i = 0; i < 6; i++)
                AddLine(vh, c, c + AxisDir(i) * r, color);
            return;
        }

        // 数据多边形: 填充(6个扇形三角, 降alpha) + 描边
        var pts = new Vector2[6];
        for (int i = 0; i < 6; i++)
            pts[i] = c + AxisDir(i) * (r * Mathf.Clamp(_values[i], 0.04f, 1f));
        var fillCol = color; fillCol.a *= 0.3f;
        for (int i = 0; i < 6; i++)
        {
            int j = (i + 1) % 6;
            vh.AddVert(c, fillCol, Vector2.zero);
            vh.AddVert(pts[i], fillCol, Vector2.zero);
            vh.AddVert(pts[j], fillCol, Vector2.zero);
        }
        for (int i = 0; i < 6; i++)
            AddLine(vh, pts[i], pts[(i + 1) % 6], color);
        // 六顶点小圆点 (小十字方块)
        var dotCol = color; dotCol.a = Mathf.Min(1f, dotCol.a * 1.2f);
        foreach (var p in pts)
        {
            float d = LineWidth * 1.6f;
            vh.AddVert(new Vector2(p.x - d, p.y - d), dotCol, Vector2.zero);
            vh.AddVert(new Vector2(p.x + d, p.y - d), dotCol, Vector2.zero);
            vh.AddVert(new Vector2(p.x + d, p.y + d), dotCol, Vector2.zero);
            vh.AddVert(new Vector2(p.x - d, p.y + d), dotCol, Vector2.zero);
            int i = vh.currentVertCount - 4;
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }
    }

    /// <summary>两点间的线段四边形 (2三角)</summary>
    private static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, Color col)
    {
        var dir = b - a;
        float len = dir.magnitude;
        if (len < 0.01f) return;
        dir /= len;
        var n = new Vector2(-dir.y, dir.x) * (LineWidth * 0.5f);
        vh.AddVert(a + n, col, Vector2.zero);
        vh.AddVert(a - n, col, Vector2.zero);
        vh.AddVert(b - n, col, Vector2.zero);
        vh.AddVert(b + n, col, Vector2.zero);
        int i = vh.currentVertCount - 4;
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i, i + 2, i + 3);
    }
}
