using UnityEngine;

namespace SplattingPipeline
{

    [RequireComponent(typeof(Camera))]
    public class RuntimeGrid : MonoBehaviour
    {
        public int gridSize = 20;      // number of lines each direction
        public float spacing = 1f;     // distance between lines
        public Color gridColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
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
                float pos = i * spacing;

                // lines parallel to X axis
                GL.Vertex3(-half, 0, pos);
                GL.Vertex3(half, 0, pos);

                // lines parallel to Z axis
                GL.Vertex3(pos, 0, -half);
                GL.Vertex3(pos, 0, half);
            }

            GL.End();
        }

        void CreateMaterialIfNeeded()
        {
            if (_lineMaterial != null) return;

            // Simple unlit transparent shader, built-in and always available
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            _lineMaterial = new Material(shader);
            _lineMaterial.hideFlags = HideFlags.HideAndDontSave;
            _lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            _lineMaterial.SetInt("_ZWrite", 0);
        }
    }
}