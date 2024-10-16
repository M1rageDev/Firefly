using System.Collections.Generic;
using UnityEngine;

namespace AtmosphericFx
{
	/// <summary>
	/// Helper class used to generate hexagon grid planes, used for the grid projection
	/// </summary>
	internal class Hexgrid
	{
		public Vector2Int gridDimensions;
		public float hexRadius;

		Vector3[] hexagon;

		Mesh mesh;
		List<Vector3> vertices;
		List<int> triangles;
		List<Vector2> uvs;

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
			vertices = new List<Vector3>();
			triangles = new List<int>();
			uvs = new List<Vector2>();

			PregenerateHexagon(hexRadius);  // Pregenerate the hexagon shape

			// Grid values
			float sqrt3 = Mathf.Sqrt(3f);
			float hexWidth = sqrt3 * hexRadius;
			float hexHeight = 2f * hexRadius;
			float xOffset = hexWidth * (sqrt3 / 2);

			for (int y = 0; y < gridDimensions.y; y++)
			{
				for (int x = 0; x < gridDimensions.x; x++)
				{
					// Calculate the position for each hexagon in a grid
					float xPos = x * xOffset;
					float yPos = y * hexHeight * (sqrt3 / 2f);

					// Offset every second column (odd columns)
					if (x % 2 == 1)
					{
						yPos += hexHeight * (sqrt3 / 4f);
					}

					// Create the hexagon
					AddHexagon(new Vector3(xPos, 0, yPos));
				}
			}

			// Create the mesh object
			mesh = new Mesh();
			mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

			mesh.vertices = vertices.ToArray();
			mesh.triangles = triangles.ToArray();
			mesh.uv = uvs.ToArray();

			Logging.Log($"Hexgrid: Created {vertices.Count} verts");
		}

		/// <summary>
		/// Initializes the object and generates the hexgrid
		/// </summary>
		public Hexgrid(Vector2Int gridDimensions, float hexRadius)
		{
			this.gridDimensions = gridDimensions;
			this.hexRadius = hexRadius;

			Generate();
		}
	}
}
