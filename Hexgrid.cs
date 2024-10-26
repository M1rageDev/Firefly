using System.Collections.Generic;
using UnityEngine;
using static KSP.UI.Screens.RDArchivesController;

namespace AtmosphericFx
{
	/// <summary>
	/// Helper class used to generate hexagon grid planes, used for the grid projection
	/// </summary>
	public class HexGrid
	{
		public Mesh HexMesh { get; private set; }

		public Vector2Int gridDimensions;
		public float hexRadius;

		Vector3[] hexagon;

		List<Vector3> vertices;
		List<int> triangles;
		List<Vector2> uvs;

		/// <summary>
		/// Returns the hexgrid dimensions for a given radius and desired grid size
		/// Rounds the results upward
		/// </summary>
		public static Vector2Int CalculateDimensions(float radius, Vector2 desiredSize)
		{
			float sqrt3 = Mathf.Sqrt(3f);
			float hexWidth = sqrt3 * radius;
			float xSpacing = hexWidth * (sqrt3 / 2f);

			int gridWidth = Mathf.CeilToInt(desiredSize.x / xSpacing);
			int gridHeight = Mathf.CeilToInt(desiredSize.y / hexWidth);

			return new Vector2Int(gridWidth, gridHeight);
		}

		/// <summary>
		/// Pregenerates the hexagon shape, with a predefined radius
		/// </summary>
		void PregenerateHexagon(float radius)
		{
			hexagon = new Vector3[7];

			// Generate the hexagon corners
			for (int i = 0; i < 6; i++)
			{
				float angle = 30f + i * 60f;
				Vector3 vertex = new Vector3(
					radius * Mathf.Sin(angle * Mathf.Deg2Rad),
					0f,
					radius * Mathf.Cos(angle * Mathf.Deg2Rad)
				);
				hexagon[i] = vertex;
			}

			// Add center vertex
			hexagon[6] = Vector3.zero;
		}

		/// <summary>
		/// Creates a hexagon from the predefined shape in a given center coordinate
		/// </summary>
		void AddHexagon(Vector3 center)
		{
			int vertexIndexStart = vertices.Count;

			// Add vertices for the hex, from the predefined hexagon shape
			for (int i = 0; i < 6; i++)
			{
				Vector3 vertex = center + hexagon[i];

				vertices.Add(vertex);
				uvs.Add(new Vector2(center.x, center.y));
			}

			// Add center vertex
			vertices.Add(center);
			uvs.Add(new Vector2(center.x, center.y));

			// Create triangles
			for (int i = 0; i < 6; i++)
			{
				triangles.Add(vertexIndexStart + i);
				triangles.Add(vertexIndexStart + (i + 1) % 6);
				triangles.Add(vertexIndexStart + 6);
			}
		}

		/// <summary>
		/// Generates the hexagon grid from the predefined parameters
		/// </summary>
		void Generate()
		{
			if (hexRadius <= 0 || gridDimensions.x < 1 || gridDimensions.y < 1) return;

			vertices = new List<Vector3>();
			triangles = new List<int>();
			uvs = new List<Vector2>();

			PregenerateHexagon(hexRadius);  // Pregenerate the hexagon shape

			// Grid values
			float sqrt3 = Mathf.Sqrt(3f);
			float hexWidth = sqrt3 * hexRadius;
			float xOffset = hexWidth * (sqrt3 / 2f);

			for (int y = 0; y < gridDimensions.y; y++)
			{
				for (int x = 0; x < gridDimensions.x; x++)
				{
					// Calculate the position for each hexagon in a grid
					float xPos = x * xOffset;
					float yPos = y * hexWidth;

					// Offset every second column (odd columns)
					if (x % 2 == 1)
					{
						yPos += hexWidth / 2f;
					}

					// Create the hexagon
					AddHexagon(new Vector3(xPos, 0, yPos));
				}
			}

			// Create the mesh object
			HexMesh = new Mesh();
			HexMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

			HexMesh.vertices = vertices.ToArray();
			HexMesh.triangles = triangles.ToArray();
			HexMesh.uv = uvs.ToArray();

			HexMesh.bounds = new Bounds(Vector3.zero, HexMesh.bounds.size * 500f);  // set the bounds to be absurdly big, to not get culled by the frustum culling

			Logging.Log($"Hexgrid: Created {vertices.Count} verts");
		}

		/// <summary>
		/// Initializes the object and generates the hexgrid
		/// </summary>
		public HexGrid(Vector2Int gridDimensions, float hexRadius)
		{
			this.gridDimensions = gridDimensions;
			this.hexRadius = hexRadius;

			Generate();
		}
	}
}
