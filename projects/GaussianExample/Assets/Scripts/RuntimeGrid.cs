using UnityEngine;

namespace SplattingPipeline
{

    [RequireComponent(typeof(Camera))]
    public class RuntimeGrid : MonoBehaviour
    {
        public int gridSize = 20;      // number of lines each direction
        public float spacing = 1f;     // distance between lines
        public Color gridColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        
        [Header("Center Marker")]
        public bool showCenterMarker = true;
        public Color centerColor = new Color(1f, 0.3f, 0.3f, 0.9f);
        public float centerMarkerRadius = 0.3f;
        public int centerMarkerSegments = 24;
        
        private Material _lineMaterial;

        void OnPostRender()
        {
            CreateMaterialIfNeeded();
            _lineMaterial.SetPass(0);

            GL.Begin(GL.LINES);
            GL.Color(gridColor);

            float half = gridSize * spacing;

            for (int i = -gridSize; i <= gridSize; i++)
            {
                if (i == 0) continue;
                
                float pos = i * spacing;

                // lines parallel to X axis
                GL.Vertex3(-half, 0, pos);
                GL.Vertex3(half, 0, pos);

                // lines parallel to Z axis
                GL.Vertex3(pos, 0, -half);
                GL.Vertex3(pos, 0, half);
            }

            if (showCenterMarker)
            {
                GL.Begin(GL.LINES);
                GL.Color(centerColor);

                GL.Vertex3(-half, 0, 0);
                GL.Vertex3(half, 0, 0);

                GL.Vertex3(0, 0, -half);
                GL.Vertex3(0, 0, half);

                GL.End();

                GL.Begin(GL.LINES);
                GL.Color(centerColor);

                for (int i = 0; i < centerMarkerSegments; i++)
                {
                    float t0 = (i / (float)centerMarkerSegments) * Mathf.PI * 2f;
                    float t1 = ((i + 1) / (float)centerMarkerSegments) * Mathf.PI * 2f;

                    Vector3 p0 = new Vector3(Mathf.Cos(t0), 0, Mathf.Sin(t0)) * centerMarkerRadius;
                    Vector3 p1 = new Vector3(Mathf.Cos(t1), 0, Mathf.Sin(t1)) * centerMarkerRadius;

                    GL.Vertex3(p0.x, 0, p0.z);
                    GL.Vertex3(p1.x, 0, p1.z);
                }
            }

            GL.End();
        }

        void CreateMaterialIfNeeded()
        {
            if (_lineMaterial != null) return;

            Shader shader = Shader.Find("Hidden/Internal-Colored");
            _lineMaterial = new Material(shader);
            _lineMaterial.hideFlags = HideFlags.HideAndDontSave;
            _lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            _lineMaterial.SetInt("_ZWrite", 0);
            // _lineMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        }
    }
}