using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Serialization;
using System.Globalization;
using System.Runtime.CompilerServices;

/// <summary>
/// Basic physics manager capable of simulating a given ISimulable
/// implementation using diverse integration methods: explicit,
/// implicit, Verlet and semi-implicit.
/// </summary>
public class ElasticSolid : MonoBehaviour 
{
	/// <summary>
	/// Default constructor. Zero all. 
	/// </summary>
	public ElasticSolid()
	{
		this.Paused = true;
		this.TimeStep = 0.02f;
		this.Substeps = 5;
		this.Gravity = new Vector3 (0.0f, -9.81f, 0.0f);
		this.IntegrationMethod = Integration.Explicit;
		this.Stiffness = 500.0f;
		this.Damping = 0.5f;
		this.MassDensity = 4.0f;
	}

	/// <summary>
	/// Integration method.
	///		0 = Explícito
	///		1 = Simpléctico
	/// </summary>
	public enum Integration
	{
		Explicit = 0,
		Symplectic = 1,
	};

	#region InEditorVariables

	//************************* PARÁMETROS DE LA SIMULACIÓN EDITABLES *************************//
	/// <summary>
	/// Variable auxiliar para controlar la simulación.
	/// </summary>
	public bool Paused;
	/// <summary>
	/// Tiempo de paso de la simulación.
	/// </summary>
	public float TimeStep;
	/// <summary>
	/// Subpasos de la simulación.
	/// </summary>
	public int Substeps;
	/// <summary>
	/// Gravedad.
	/// </summary>
    public Vector3 Gravity;
	/// <summary>
	/// Método de integración.
	/// </summary>
	public Integration IntegrationMethod;

	//************************* LISTAS DE ELEMENTOS DE SIMULACIÓN *************************//
	/// <summary>
	/// Lista de nodos del objeto.
	/// </summary>
	public List<Node> Nodes  = new List<Node>();
	/// <summary>
	/// Lista de muelles en el objeto.
	/// </summary>
	public List<Spring> Springs = new List<Spring>();
	/// <summary>
	/// Diccionario de aristas de la malla del objeto.
	/// </summary>
	public Dictionary<string,Edge> Edges;

    #endregion
    
    #region SimulationVariables
    
    /// <summary>
    /// Rigidez de los muelles de tracción.
    /// </summary>
    private float Stiffness;
    /// <summary>
    /// Amortiguamiento.
    /// </summary>
    private float Damping;
    /// <summary>
    /// Amortiguamiento.
    /// </summary>
    private float MassDensity;
    
    #endregion

    #region ObjectVariables
	//************************* INFORMACIÓN DEL OBJETO *************************//
    /// <summary>
    /// Malla del objeto.
    /// </summary>
    private Mesh ObjectMesh;
    /// <summary>
    /// Array de vértices de la malla.
    /// </summary>
    private Vector3[] Vertices;
    private List<Vector3> BarVertices;
    /// <summary>
    /// Array de tetraedros de la malla.
    /// </summary>
    private List<Tetrahedron> Tetrahedrons;
    
    #endregion

    #region TFVariables

    /// <summary>
    /// Asset correspondiente a fichero de los parámetros de simulación.
    /// </summary>
    public TextAsset ParametersFile;
    /// <summary>
    /// Asset correspondiente a fichero de nodos de la malla de simulación.
    /// </summary>
    public TextAsset NodeFile;
    /// <summary>
    /// Asset correspondiente a fichero de tetraedros de la malla de simulación.
    /// </summary>
    public TextAsset TetraFile;
    
    #endregion

    #region MonoBehaviour

    public void Awake()
    {
	    // INICIALIZACIÓN DE PARÁMETROS //
	    // Ajuste del paso de tiempo en función de los subpasos
	    TimeStep /= Substeps;
	    // Obtención malla del objeto a animar
	    ObjectMesh = this.GetComponentInChildren<MeshFilter>().mesh;
	    Vertices = ObjectMesh.vertices;
	    // Inicialización lista de vértices con coordenadas baricéntricas
	    BarVertices = new List<Vector3>();
	    // Inicialización de lista de tetraedros
	    Tetrahedrons = new List<Tetrahedron>();
	    // Obtención de datos de ficheros
	    string[] parametersString = ParametersFile.text.Split(new string[] { " ", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);	// Fichero de parámetros
	    string[] meshNodesString = NodeFile.text.Split(new string[] { " ", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);	// Fichero de nodos
	    string[] meshTetrasString = TetraFile.text.Split(new string[] { " ", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);	// Fichero de tetraedros
	    
	    // LECTURA DE FICHEROS //
	    // Cambio de cultura para lectura de fichero
	    CultureInfo locale = new CultureInfo("en-US");
	    // Obtención de parámetros de simulación
	    int totalParameters = int.Parse(parametersString[0]);
	    for (int i = 1; i <= totalParameters; i++)
	    {
		    string actualParameter = parametersString[2 * i + 0];
		    // Asignación de valor en función del parámetro actual en la lectura del fichero
		    switch (actualParameter)
		    {
			    case "Stiffness":
				    Stiffness = float.Parse(parametersString[2 * i + 1], locale);
				    break;
			    case "Damping":
				    Damping = float.Parse(parametersString[2 * i + 1], locale);
				    break;
			    case "MassDensity":
				    MassDensity = float.Parse(parametersString[2 * i + 1], locale);
				    break;
		    }
	    }
	    
	    // Obtención de nodos
	    int totalNodes = int.Parse(meshNodesString[0]);
	    for (int i = 1; i <= totalNodes; i++)
	    {
		    // Cálculo de posición del nodo
		    Vector3 nodePosition = new Vector3(
			    float.Parse((meshNodesString[4 * i + 1]), locale),
			    float.Parse((meshNodesString[4 * i + 2]), locale),
			    float.Parse((meshNodesString[4 * i + 3]), locale)
			);

		    // Inserción de un nuevo nodo
		    Nodes.Add(new Node(nodePosition, Damping, this));
	    }

	    // Obtención de tetraedos (para muelles)
	    int totalTetrahedrons = int.Parse(meshTetrasString[0]);
	    for (int i = 1; i <= totalTetrahedrons; i++)
	    {
		    // Generación de tetraedo auxiliar a almacenar
		    Tetrahedron auxTetra = new Tetrahedron(int.Parse(meshTetrasString[5 * i - 2]) - 1);
		    
		    // Obtención e inserción de índices de nodos que lo conforman
		    int n0 = int.Parse(meshTetrasString[5 * i - 1], locale) - 1;
		    auxTetra.tetraNodes.Add(n0);
		    int n1 = int.Parse(meshTetrasString[5 * i + 0], locale) - 1;
		    auxTetra.tetraNodes.Add(n1);
		    int n2 = int.Parse(meshTetrasString[5 * i + 1], locale) - 1;
		    auxTetra.tetraNodes.Add(n2);
		    int n3 = int.Parse(meshTetrasString[5 * i + 2], locale) - 1;
		    auxTetra.tetraNodes.Add(n3);

		    // Cálculo de normales
		    // Plano x0x3x2
		    auxTetra.tetraPlanes.Add(n0, Vector3.Cross(
				Nodes[n3].Pos - Nodes[n0].Pos,			// Vector x0x3
			    Nodes[n2].Pos - Nodes[n0].Pos).normalized);		// Vector x0x2
		    // Plano x1x2x3
		    auxTetra.tetraPlanes.Add(n1, Vector3.Cross(
			    Nodes[n2].Pos - Nodes[n1].Pos,			// Vector x1x2
			    Nodes[n3].Pos - Nodes[n1].Pos).normalized);		// Vector x1x3
		    // Plano x2x1x0
		    auxTetra.tetraPlanes.Add(n2, Vector3.Cross(
			    Nodes[n1].Pos - Nodes[n2].Pos,			// Vector x2x1
			    Nodes[n0].Pos - Nodes[n2].Pos).normalized);		// Vector x2x0
		    // Plano x3x0x1
		    auxTetra.tetraPlanes.Add(n3, Vector3.Cross(
			    Nodes[n0].Pos - Nodes[n3].Pos,			// Vector x3x0
			    Nodes[n1].Pos - Nodes[n3].Pos).normalized);		// Vector x3x1
		    
		    // Cálculo de volumen
		    auxTetra.CalculateVolume(Nodes);
		    // Cálculo de masas
		    auxTetra.CalculateMass(MassDensity);
		    auxTetra.CalculateNodesMass(Nodes);

		    // Inserción de tetraedro auxiliar a lista de tetraedros
		    Tetrahedrons.Add(auxTetra);
		    
		    // Implementación de muelles
		    Springs.Add(new Spring(Nodes[n0], Nodes[n1], Stiffness, Damping));
		    Springs.Add(new Spring(Nodes[n0], Nodes[n2], Stiffness, Damping));
		    Springs.Add(new Spring(Nodes[n0], Nodes[n3], Stiffness, Damping));
		    Springs.Add(new Spring(Nodes[n1], Nodes[n2], Stiffness, Damping));
		    Springs.Add(new Spring(Nodes[n1], Nodes[n3], Stiffness, Damping));
		    Springs.Add(new Spring(Nodes[n2], Nodes[n3], Stiffness, Damping));
	    }
	    
	    // Comprobación de inclusión de vértices en tetraedros
	    foreach (Vector3 vertex in Vertices)
	    {
		    // Cálculo de coordenadas globales
		    Vector3 globalVertex = transform.TransformPoint(vertex);
		    foreach (Tetrahedron tetra in Tetrahedrons)
		    {
			    // Si no se encuentra el vértice en el tetraedro
			    if (!tetra.ContainsVertex(Nodes, globalVertex)) continue;
			    
			    // Cálculo de coordenadas baricéntricas
			    tetra.CalculateBarycentricCoords(Nodes, globalVertex);
		    }
	    }

	    // Cálculo de las nuevas posiciones
	    foreach (Vector3 vertex in Vertices)
	    {
		    foreach (Tetrahedron tetra in Tetrahedrons)
		    {
			    if(!tetra.tetraVertices.Contains(vertex)) continue;

			    int index = tetra.GetIndexFromVertexList(vertex);
			    Vector3 newVertex = tetra.UpdateVertex(Nodes, vertex); // Actualizar uV para devolver un solo dado un vertice
			    tetra.tetraVertices[index] = newVertex;
			    
			    BarVertices.Add(newVertex);
		    }
	    }

	    AssignVertices();
    }

    /// <summary>
    /// Asignación de vértices en baricéntricas a la malla del objeto.
    /// </summary>
    public void AssignVertices()
    {
	    for (int i = 0; i < BarVertices.Count; i++)
	    {
		    // Cálculo de coordenadas locales
		    Vertices[i] = transform.InverseTransformPoint(BarVertices[i]);
	    }

	    // Asignación a la malla
	    ObjectMesh.vertices = Vertices;
    }
    
    /// <summary>
    /// Dibujado de gizmos para la representación de la malla de simulación.
    /// </summary>
    public void OnDrawGizmos()
    {
	    // Dibujado de esferas (nodos)
	    Gizmos.color = Color.blue;
	    foreach (Node node in Nodes)
	    {
		    Gizmos.DrawSphere(node.Pos, 0.2f);
	    }

	    // Dibujado de aristas (muelles)
	    Gizmos.color = Color.red;
	    foreach (Spring spring in Springs)
	    {
		    Gizmos.DrawLine(spring.NodeA.Pos, spring.NodeB.Pos);
	    }
    }

    public void Update()
	{
		if (Input.GetKeyUp (KeyCode.P))
			this.Paused = !this.Paused;
		
		BarVertices.Clear();
		foreach (Vector3 vertex in Vertices)
		{
			foreach (Tetrahedron tetra in Tetrahedrons)
			{
				if(!tetra.tetraVertices.Contains(vertex)) continue;

				int index = tetra.GetIndexFromVertexList(vertex);
				Vector3 newVertex = tetra.UpdateVertex(Nodes, vertex); // Actualizar uV para devolver un solo dado un vertice
				tetra.tetraVertices[index] = newVertex;
			    
				BarVertices.Add(newVertex);
			}
		}

		AssignVertices();
	}

	// FixedUpdate se llama 50 veces por segundo, por lo que se llama cada 0,02s
    public void FixedUpdate()
    {
        if (this.Paused)
            return; // Not simulating
        
        // Ejecución de simulación por cada subpaso hasta llegar a los establecidos
        for (int i = 0; i < Substeps; i++)
        {
	        switch (this.IntegrationMethod)
	        {
		        // Select integration method
		        case Integration.Explicit: this.StepExplicit(); break;
		        case Integration.Symplectic: this.StepSymplectic(); break;
		        default:
			        throw new System.Exception("[ERROR] Should never happen!");
	        }
        }
    }

    #endregion

    /// <summary>
    /// Performs a simulation step in 1D using Explicit integration.
    /// </summary>
    private void StepExplicit()
	{
		// Paso 0 - Reset de fuerzas de los nodos
		foreach (Node node in Nodes)
		{
			node.Force = Vector3.zero;
		}
		
		// Paso 1 - Cálculo de fuerzas
		// Nodos
		foreach (Node node in Nodes)
		{
			node.ComputeForces();
		}
		// Muelles
		foreach (Spring spring in Springs)
		{
			spring.ComputeForces();
		}

		// Paso 2 - Integración en el tiempo
		foreach (Node node in Nodes)
		{
			// Si es un nodo fijo continuamos al sigu
			if(node.Fixed)	continue;
			
			// Paso 2.1 - Actualización de posición de cada nodo: x(t+h) = x(t) + h * v(t+h)
			node.Pos = node.Pos + TimeStep * node.Vel;
			
			// Paso 2.2 - Actualización de velocidad de cada nodo: v(t+h) = v(t) + h*(1/m)*F(t)
			node.Vel = node.Vel + TimeStep * (1 / node.Mass) * node.Force;
		}

		// Paso 3 - Actualización longitud del muelle
		foreach (Spring spring in Springs)
		{
			spring.UpdateLength();
		}
	}

	/// <summary>
	/// Performs a simulation step in 1D using Symplectic integration.
	/// </summary>
	private void StepSymplectic()
	{
		// Paso 0 - Reset de fuerzas de los nodos
		foreach (Node node in Nodes)
		{
			node.Force = Vector3.zero;
		}
		
		// Paso 1 - Cálculo de fuerzas
		// Nodos
		foreach (Node node in Nodes)
		{
			node.ComputeForces();
		}
		// Muelles
		foreach (Spring spring in Springs)
		{
			spring.ComputeForces();
		}
		
		// Paso 2 - Integración en el tiempo
		foreach (Node node in Nodes)
		{
			// Si es un nodo fijo continuamos al sigu
			if(node.Fixed)	continue;
			
			// Paso 2.1 - Actualización de velocidad de cada nodo: v(t+h) = v(t) + h*(1/m)*F(t)
			node.Vel = node.Vel + TimeStep * (1 / node.Mass) * node.Force;
			
			// Paso 2.2 - Actualización de posición de cada nodo: x(t+h) = x(t) + h * v(t+h)
			node.Pos = node.Pos + TimeStep * node.Vel;
		}

		// Paso 3 - Actualización longitud del muelle
		foreach (Spring spring in Springs)
		{
			spring.UpdateLength();
		}
	}
}
