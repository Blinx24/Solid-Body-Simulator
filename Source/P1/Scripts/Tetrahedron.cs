using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using Vector3 = UnityEngine.Vector3;

public class Tetrahedron
{
    // Variables
    #region Vars
    
    /// <summary>
    /// ID del tetraedro.
    /// </summary>
    public int Id;
    /// <summary>
    /// Masa del tetraedro.
    /// </summary>
    public float mass;
    /// <summary>
    /// Volumen del tetraedro.
    /// </summary>
    public float volume;
    /// <summary>
    /// Nodos que conforman el tetraedro.
    /// </summary>
    public List<int> tetraNodes;
    /// <summary>
    /// Nodos que conforman el tetraedro.
    /// </summary>
    public Dictionary<int, Vector3> tetraPlanes;
    /// <summary>
    /// Nodos que conforman el tetraedro.
    /// </summary>
    public List<Vector3> tetraVertices;
    /// <summary>
    /// Pesos de los vértices incluidos en el tetraedro.
    /// </summary>
    public List<float[]> weightVertices;

    #endregion
    
    #region ClassConstructor

    /// <summary>
    /// Constructor clase Tetraedro.
    /// </summary>
    /// <param name="code">Identificador del tetraedro.</param>
    public Tetrahedron(int code)
    {
        Id = code;
        tetraNodes = new List<int>();
        mass = 0.0f;
        volume = 0.0f;
        tetraPlanes = new Dictionary<int, Vector3>();
        tetraVertices = new List<Vector3>();
        weightVertices = new List<float[]>();
    }
    
    #endregion
    
    #region ClassMethods

    /// <summary>
    /// Comprueba si el tetraedro contiene al vértice P.
    /// </summary>
    /// <param name="nodes">Lista de nodos de la malla de simulación.</param>
    /// <param name="p">Vértice de la malla de visualización.</param>
    public bool ContainsVertex(List<Node> nodes, Vector3 p)
    {
        // VARIABLE CONTROLADORA //
        int planeCount = 0; // Número de caras que quedan por encima de la vértice
        
        // COMPROBACIÓN DE POSICIÓN //
        if (Vector3.Dot(
            tetraPlanes[tetraNodes[0]],
            p - nodes[tetraNodes[0]].Pos
        ) <= 0)
            planeCount++;

        if (Vector3.Dot(
            tetraPlanes[tetraNodes[1]],
            p - nodes[tetraNodes[1]].Pos
        ) <= 0)
            planeCount++;

        if (Vector3.Dot(
            tetraPlanes[tetraNodes[2]],
            p - nodes[tetraNodes[2]].Pos
        ) <= 0)
            planeCount++;

        if (Vector3.Dot(
            tetraPlanes[tetraNodes[3]],
            p - nodes[tetraNodes[3]].Pos
        ) <= 0)
            planeCount++;

        // COMPROBACIÓN VARIABLE //
        if (planeCount == 4)
        {
            tetraVertices.Add(p);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Cálculo de la masa del tetraedo.
    /// </summary>
    /// <param name="md">Masa de densidad del objeto.</param>
    public void CalculateMass(float md)
    {
        mass = md * volume;
    }

    /// <summary>
    /// Distribución de la masa entre los nodos del tetraedro.
    /// </summary>
    /// <param name="nodes">Lista de nodos.</param>
    public void CalculateNodesMass(List<Node> nodes)
    {
        foreach (int index in tetraNodes)
        {
            nodes[index].Mass = mass / 4.0f;
        }
    }
    
    /// <summary>
    /// Cálculo del volumen del tetraedro.
    /// </summary>
    /// <param name="nodes">Lista de nodos.</param>
    public void CalculateVolume(List<Node> nodes)
    {
        float value = Math.Abs(
            Vector3.Dot(
                nodes[tetraNodes[1]].Pos - nodes[tetraNodes[0]].Pos,
                Vector3.Cross(
                    nodes[tetraNodes[2]].Pos - nodes[tetraNodes[0]].Pos,
                    nodes[tetraNodes[3]].Pos - nodes[tetraNodes[0]].Pos))
        );
        
        volume = value / 6.0f;
    }

    /// <summary>
    /// Cálculo del volumen del tetraedro interno.
    /// </summary>
    /// <param name="nodes">Lista de nodos.</param>
    /// <param name="excludedNode">Nodo a excluir del cálculo del voluem (opuesto al tetraedro pequeño).</param>
    /// <param name="p">Vértice de la malla de visualización.</param>
    public float CalculateSubVolume(List<Node> nodes, int excludedNode, Vector3 p)
    {
        // Variable auxiliar para el valor del volumen
        float value = 0.0f;
        
        // Cálculo del subvolumen en función del nodo del sumatorio
        switch (excludedNode)
        {
            case 0:
                value = Math.Abs(
                    Vector3.Dot(
                        nodes[tetraNodes[1]].Pos - p,
                        Vector3.Cross(
                            nodes[tetraNodes[2]].Pos - p,
                            nodes[tetraNodes[3]].Pos - p))
                );
                
                break;
            case 1:
                value = Math.Abs(
                    Vector3.Dot(
                        p - nodes[tetraNodes[0]].Pos,
                        Vector3.Cross(
                            nodes[tetraNodes[2]].Pos - nodes[tetraNodes[0]].Pos,
                            nodes[tetraNodes[3]].Pos - nodes[tetraNodes[0]].Pos))
                );
                
                break;
            case 2:
                value = Math.Abs(
                    Vector3.Dot(
                        nodes[tetraNodes[1]].Pos - nodes[tetraNodes[0]].Pos,
                        Vector3.Cross(
                            p - nodes[tetraNodes[0]].Pos,
                            nodes[tetraNodes[3]].Pos - nodes[tetraNodes[0]].Pos))
                );
                
                break;
            case 3:
                value = Math.Abs(
                    Vector3.Dot(
                        nodes[tetraNodes[1]].Pos - nodes[tetraNodes[0]].Pos,
                        Vector3.Cross(
                            nodes[tetraNodes[2]].Pos - nodes[tetraNodes[0]].Pos,
                            p - nodes[tetraNodes[0]].Pos))
                );
                
                break;
        }

        value /= 6.0f;
        
        return value;
    }
    
    /// <summary>
    /// Calcular e insertar coordenadas baricéntricas de vértices incluidos en el tetraedro.
    /// </summary>
    /// <param name="nodes">Lista de nodos de la malla de simulación.</param>
    /// <param name="p">Vértice de la malla de visualización.</param>
    public void CalculateBarycentricCoords(List<Node> nodes, Vector3 p)
    {
        float[] weights = new float[4];

        for (int i = 0; i < tetraNodes.Count; i++)
        {
            float subVolume = CalculateSubVolume(nodes, i, p);
            float w = subVolume / volume;

            weights[i] = w;
        }
        
        weightVertices.Add(weights);
    }

    /// <summary>
    /// Actualizar posición del vértice introducido.
    /// </summary>
    /// <param name="nodes">Lista de nodos de la malla de simulación.</param>
    /// <param name="v">Vértice de la malla de visualización.</param>
    public Vector3 UpdateVertex(List<Node> nodes, Vector3 v)
    {
        // Obtención del índice del vértice en la lista
        int i = GetIndexFromVertexList(v);
        // Inicialización de vector auxiliar
        Vector3 bVertex = Vector3.zero;

        // Cálculo de nuevas coordenadas a raíz de los nodos que conforman el tetraedro
        for (int j = 0; j < tetraNodes.Count; j++)
        {
            // Nueva posición = peso * posición del nodo
            bVertex += weightVertices[i][j] * nodes[tetraNodes[j]].Pos;
        }

        // Devolución de la nueva posición
        return bVertex;
    }

    /// <summary>
    /// Obtener índice del vértice en la lista de vértices contenidos.
    /// </summary>
    /// <param name="v">Vértice de la malla de visualización.</param>
    public int GetIndexFromVertexList(Vector3 v)
    {
        // Inicialización del posible índice del vector
        int index = -1;

        // Búsqueda en lista de vectores contenidos
        for (int i = 0; i < tetraVertices.Count; i++)
        {
            // Si el vértice introducido coincide con el vértice consultado actual
            if (tetraVertices[i].x == v.x && tetraVertices[i].y == v.y && tetraVertices[i].z == v.z)
            {
                // Se asigna el índice de i al auxiliar
                index = i;
                break;
            }
        }

        // Devolución del índice
        return index;
    }

    #endregion
}
