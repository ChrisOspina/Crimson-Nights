using Unity.AI.Navigation;
using UnityEngine;

// Bakes the attached NavMeshSurface when the scene starts, before enemies look for the NavMesh.
// Fine for small prototype arenas; switch to an editor-baked NavMesh once levels get bigger.
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(NavMeshSurface))]
public class RuntimeNavMeshBaker : MonoBehaviour
{
	private void Awake()
	{
		GetComponent<NavMeshSurface>().BuildNavMesh();
	}
}
