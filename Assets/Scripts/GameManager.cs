using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;
public class GameManager : MonoBehaviour
{
    public Text Score, Blocks;
    public bool stopAtFirstHit = false;
    public Material visitedMaterial = null;
    
    public enum Heuristics { Euclidean, Manhattan, Bisector, FullBisector, Zero };
    public HeuristicFunction[] myHeuristics = { EuclideanEstimator, ManhattanEstimator, BisectorEstimator,
                                                 FullBisectorEstimator, ZeroEstimator };
    public Heuristics heuristicToUse = Heuristics.Euclidean;
    public int x = 10;
    public int y = 10;
    public int speed=5, blocks=5;
    [Range(0f, 1f)] public float edgeProbability = 0.75f;
    public int RandomSeed = 0;
    public Color edgeColor = Color.red;
    public float gap = 2f;
    public Material startMaterial = null;
    public Material trackMaterial = null;
    public Material endMaterial = null;
    public float delay = 0.5f;
    // what to put on the scene, not really meaningful
    public GameObject sceneObject;
    List<Edge> totalPath = new List<Edge>();
    protected Node[,] matrix;
    protected Graph g;
    bool done = false;

    void Start()
    {
        
        if (sceneObject != null)
        {
            if (Scenes.getParam("gridLenght") != "")
            {
                x = int.Parse(Scenes.getParam("gridLenght"));
                y = int.Parse(Scenes.getParam("gridHeight"));
                delay = 1.2f-float.Parse(Scenes.getParam("speed"))/10;
                blocks = int.Parse(Scenes.getParam("blocks"));
                Blocks.text = "" + 5;
            }
            // create a x * y matrix of nodes (and scene objects)
            // edge weight is now the geometric distance (gap)
            matrix = CreateGrid(sceneObject, x, y, gap);

            // create a graph and put random edges inside
            g = new Graph();

            // initialize randomness, so experiments can be repeated
            //if (RandomSeed == 0) RandomSeed = (int)System.DateTime.Now.Ticks;
            //Random.InitState(RandomSeed);
            //CreateLabyrinth(g, matrix, edgeProbability);
            CreateGraph(g, matrix);
            // ask A* to solve the problem
            AStarStepSolver.immediateStop = stopAtFirstHit;
            AStarStepSolver.Init(g, matrix[0, 0], matrix[x - 1, y - 1], myHeuristics[(int)heuristicToUse]);

            // Outline visited nodes

            StartCoroutine(AnimateSolution(delay));
            StartCoroutine(BlocksRecovery(10/blocks));
        }
    }

    void Update()
    {
        if (Input.touchCount == 1 && Input.GetTouch(0).phase == 0)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.GetTouch(0).position);
            RaycastHit hit;
            Debug.DrawRay(ray.origin, ray.direction * 100, Color.yellow, 100f);
            if (Physics.Raycast(ray, out hit))
            {
                Debug.Log(hit.transform.name);
                if (hit.collider != null&&int.Parse(Blocks.text)>0)
                {
                    bool found=false;
                    Blocks.text=""+(int.Parse(Blocks.text) - 1);
                    GameObject touchedObject = hit.transform.gameObject;
                    Node n = g.getNode(touchedObject.name);
                    foreach(Edge e in totalPath)
                    {
                        if (e.from == n || e.to == n) found = true;
                    }
                    if (!found)
                    {
                        OutlineNode(n, startMaterial);
                        Debug.Log(n.description);
                        g.RemoveNode(n);
                    }
                }
            }
        }
    }
    private IEnumerator BlocksRecovery(float pause)
    {
        while (!done)
        {
            yield return new WaitForSeconds(pause);
            Blocks.text = "" + (int.Parse(Blocks.text) + 1);
        }
    }

    private IEnumerator AnimateSolution(float pause)
    {
        while (!done) { 

            while (AStarStepSolver.Step())
            {
                OutlineSet(AStarStepSolver.visited, visitedMaterial);
                //OutlineNode(AStarStepSolver.current, trackMaterial);
            }
            Edge[] path = AStarStepSolver.solution;
            // check if there is a solution
            if (path.Length == 0)
            {
                UnityEditor.EditorUtility.DisplayDialog("Sorry, No solution", "Score: "+ Score.text, "OK");
                done = true;
            }
            else
            {
                // if yes, outline it
                totalPath.Add(path[0]);
                OutlinePath(totalPath.ToArray(), startMaterial, trackMaterial, endMaterial);
                if (path[0].to == matrix[x - 1, y - 1])
                {
                    UnityEditor.EditorUtility.DisplayDialog("Sorry, End of the Run", "Score: " + Score.text, "OK");
                    done = true;
                }

            }
            AStarStepSolver.Init(g, totalPath[totalPath.Count-1].to, matrix[x - 1, y - 1], myHeuristics[(int)heuristicToUse]);
            Score.text = ""+(int.Parse(Score.text) + 1);
            yield return new WaitForSeconds(pause);
        }
        Scenes.Load("MainMenu");
    }

    protected void OutlineNode(Node n, Material m)
    {
        if (n != null)
            n.sceneObject.GetComponent<MeshRenderer>().material = m;
    }

    protected void OutlineSet(List<Node> set, Material m)
    {
        if (m == null) return;
        foreach (Node n in set)
        {
            n.sceneObject.GetComponent<MeshRenderer>().material = m;
        }
    }
    protected void OutlinePath(Edge[] path, Material sm, Material tm, Material em)
    {
        if (path.Length == 0) return;
        foreach (Edge e in path)
        {
            e.to.sceneObject.GetComponent<MeshRenderer>().material = tm;
        }
        path[0].from.sceneObject.GetComponent<MeshRenderer>().material = sm;
        path[path.Length - 1].to.sceneObject.GetComponent<MeshRenderer>().material = em;
    }

    protected static float EuclideanEstimator(Node from, Node to)
    {
        return (from.sceneObject.transform.position - to.sceneObject.transform.position).magnitude;
    }

    protected static float ManhattanEstimator(Node from, Node to)
    {
        return (
                Mathf.Abs(from.sceneObject.transform.position.x - to.sceneObject.transform.position.x) +
                Mathf.Abs(from.sceneObject.transform.position.z - to.sceneObject.transform.position.z)
            );
    }

    protected static float BisectorEstimator(Node from, Node to)
    {
        Ray r = new Ray(Vector3.zero, to.sceneObject.transform.position);
        return Vector3.Cross(r.direction, from.sceneObject.transform.position - r.origin).magnitude;
    }

    protected static float FullBisectorEstimator(Node from, Node to)
    {
        Ray r = new Ray(Vector3.zero, to.sceneObject.transform.position);
        Vector3 toBisector = Vector3.Cross(r.direction, from.sceneObject.transform.position - r.origin);
        return toBisector.magnitude + (to.sceneObject.transform.position - (from.sceneObject.transform.position + toBisector)).magnitude;
    }

    protected static float ZeroEstimator(Node from, Node to) { return 0f; }

    protected virtual Node[,] CreateGrid(GameObject o, int x, int y, float gap)
    {
        Node[,] matrix = new Node[x, y];
        for (int i = 0; i < x; i += 1)
        {
            for (int j = 0; j < y; j += 1)
            {
                matrix[i, j] = new Node(i, j, Instantiate(o));
                matrix[i, j].sceneObject.name = ""+i+","+j;
                matrix[i, j].sceneObject.transform.position =
                    transform.position +
                    transform.right * gap * (i - ((x - 1) / 2f)) +
                    transform.forward * gap * (j - ((y - 1) / 2f));
                matrix[i, j].sceneObject.transform.rotation = transform.rotation;
            }
        }
        return matrix;
    }

    protected void CreateLabyrinth(Graph g, Node[,] crossings, float threshold)
    {
        for (int i = 0; i < crossings.GetLength(0); i += 1)
        {
            for (int j = 0; j < crossings.GetLength(1); j += 1)
            {
                g.AddNode(crossings[i, j]);
                foreach (Edge e in RandomEdges(crossings, i, j, threshold))
                {
                    g.AddEdge(e);
                }
            }
        }
    }
    protected void CreateGraph(Graph g, Node[,] crossings)
    {
        for (int i = 0; i < crossings.GetLength(0); i += 1)
        {
            for (int j = 0; j < crossings.GetLength(1); j += 1)
            {
                g.AddNode(crossings[i, j]);
                foreach (Edge e in RandomEdges(crossings, i, j, 1))
                {
                    g.AddEdge(e);
                }
            }
        }
    }


    protected Edge[] RandomEdges(Node[,] matrix, int x, int y, float threshold)
    {
        List<Edge> result = new List<Edge>();
        if (x != 0 && Random.Range(0f, 1f) <= threshold)
            result.Add(new Edge(matrix[x, y], matrix[x - 1, y], Distance(matrix[x, y], matrix[x - 1, y])));

        if (y != 0 && Random.Range(0f, 1f) <= threshold)
            result.Add(new Edge(matrix[x, y], matrix[x, y - 1], Distance(matrix[x, y], matrix[x, y - 1])));

        if (x != (matrix.GetLength(0) - 1) && Random.Range(0f, 1f) <= threshold)
            result.Add(new Edge(matrix[x, y], matrix[x + 1, y], Distance(matrix[x, y], matrix[x + 1, y])));

        if (y != (matrix.GetLength(1) - 1) && Random.Range(0f, 1f) <= threshold)
            result.Add(new Edge(matrix[x, y], matrix[x, y + 1], Distance(matrix[x, y], matrix[x, y + 1])));

        return result.ToArray();
    }

    protected virtual float Distance(Node from, Node to)
    {
        return 1f;
    }

    void OnDrawGizmos()
    {
        if (matrix != null)
        {
            Gizmos.color = edgeColor;
            for (int i = 0; i < x; i += 1)
            {
                for (int j = 0; j < y; j += 1)
                {
                    foreach (Edge e in g.getConnections(matrix[i, j]))
                    {
                        Vector3 from = e.from.sceneObject.transform.position;
                        Vector3 to = e.to.sceneObject.transform.position;
                        Gizmos.DrawSphere(from + ((to - from) * .2f), .2f);
                        Gizmos.DrawLine(from, to);
                    }
                }
            }
        }
    }
}