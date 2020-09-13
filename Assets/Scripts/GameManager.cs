using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;
public class GameManager : MonoBehaviour
{
    public GameObject EndButton;
    public GameObject Spawner;
    public Text Score, Blocks;
    public Material visitedMaterial = null, defaultMaterial = null;
    Node currentNode = null;
    public enum Heuristics { Euclidean, Manhattan, Bisector, FullBisector, Zero };
    public HeuristicFunction[] myHeuristics = { EuclideanEstimator, ManhattanEstimator, BisectorEstimator, FullBisectorEstimator, ZeroEstimator };
    public Heuristics heuristicToUse = Heuristics.Euclidean;
    public int x = 10, y = 10, speed=5, blocks=5, RandomSeed = 0;
    [Range(0f, 1f)] public float edgeProbability = 0.75f;
    public Color edgeColor = Color.red;
    public float gap = 2f, delay = 0.3f, acceleration = 1.008f;
    public Material startMaterial = null, trackMaterial = null, npcMaterial = null, endMaterial = null, boostMaterial = null, freezeMaterial = null, freezeBlockMaterial = null;
    // what to put on the scene, not really meaningful
    public GameObject sceneObject;
    List<Edge> totalPath = new List<Edge>();
    protected Node[,] matrix;
    protected Graph g;
    bool done = false, boost = false, freeze = false, start = false, dijkstra = false, blockRegeneration = true, climbing = false, stopAtFirstHit = false;
    int boostCount = 0, freezeCount = 0;
    private int xStart = 0, yStart = 0, xEnd = 0, yEnd = 0;
    List<Node> boostList = new List<Node>(), freezeList = new List<Node>(), blockList = new List<Node>();
    float startingDelay;
    void Start()
    {
        RandomSeed = (int)System.DateTime.Now.Ticks;
        Random.InitState(RandomSeed);
        if (sceneObject != null)
        {
            if (Scenes.getParam("gridLenght") != "")
            {
                x = int.Parse(Scenes.getParam("gridLenght"));
                y = int.Parse(Scenes.getParam("gridHeight"));
                xStart= int.Parse(Scenes.getParam("xStart"))-1;
                yStart= int.Parse(Scenes.getParam("yStart"))-1;
                xEnd= int.Parse(Scenes.getParam("xEnd"))-1;
                yEnd= int.Parse(Scenes.getParam("yEnd"))-1;
                delay = 0.35f-float.Parse(Scenes.getParam("speed"))/100;
                blocks = int.Parse(Scenes.getParam("blocks"));
                boostCount = int.Parse(Scenes.getParam("boost")) ;
                freezeCount = int.Parse(Scenes.getParam("freeze"));
                acceleration = 1+float.Parse(Scenes.getParam("acceleration"))/1000f;
                blockRegeneration = bool.Parse(Scenes.getParam("blockRegeneration"));
                climbing = bool.Parse(Scenes.getParam("Climbing"));

                if (boostCount > 0) boost=true;
                if (freezeCount > 0) freeze = true;
                if (!blockRegeneration) Blocks.text = "" + blocks;
                else Blocks.text = "" + 5;
            }
            startingDelay = delay;
            // create a x * y matrix of nodes (and scene objects)
            // edge weight is now the geometric distance (gap)
            matrix = CreateGrid(sceneObject, x, y, gap);
            // create a graph and put random edges inside
            g = new Graph();
            CreateGraph(g, matrix);
            currentNode = matrix[xStart, yStart];
            if (boost) insertSpecialBlocks(matrix, boostCount, 1, 1, boostMaterial, boostList);
            if (freeze) insertSpecialBlocks(matrix, freezeCount, 4, 8, freezeBlockMaterial, freezeList);

            matrix[xEnd, yEnd].sceneObject.GetComponent<MeshRenderer>().material = endMaterial;
            
            float xCoord = (matrix[0, 0].sceneObject.transform.position.x + matrix[matrix.GetLength(0) - 1, 0].sceneObject.transform.position.x) / 2;
            float zCoord = (matrix[0, 0].sceneObject.transform.position.z + matrix[0, matrix.GetLength(1) - 1].sceneObject.transform.position.z) / 2;
            float yCoord = (Mathf.Max(matrix[matrix.GetLength(0) - 1, 0].sceneObject.transform.position.x - matrix[0, 0].sceneObject.transform.position.x,
                 matrix[0, matrix.GetLength(1) - 1].sceneObject.transform.position.z - matrix[0, 0].sceneObject.transform.position.z)/3.5f*-Mathf.Tan(30)/2f)+18;
            Vector3 cameraPosition = new Vector3(xCoord,yCoord, zCoord);

            GameObject.Find("Main Camera").transform.position = cameraPosition;

            OutlineNode(matrix[xStart, yStart], npcMaterial);
        }
        start = true;
    }

    void Update()
    {
        Spawner.transform.position = Vector3.Lerp(Spawner.transform.position, currentNode.sceneObject.transform.position + Vector3.up * (1.5f + currentNode.height/1.5f), .2f);
        if (start && ((Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Ended) || Input.GetMouseButtonDown(0)))
        {
            if(boost||climbing) dijkstra = true;
            StartCoroutine(AnimateSolution());
            if (blockRegeneration) StartCoroutine(BlocksRecovery(10 / blocks));
            start = false;
        }
        if ((Input.touchCount == 1 && Input.GetTouch(0).phase == 0 || Input.GetMouseButtonDown(0)))
        {
            Ray ray;
            if (Input.touchCount == 1 && Input.GetTouch(0).phase == 0) ray = Camera.main.ScreenPointToRay(Input.GetTouch(0).position);
            else ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            Debug.DrawRay(ray.origin, ray.direction * 100, Color.yellow, 100f);
            if (Physics.Raycast(ray, out hit))
            {
                if (hit.collider != null)
                {
                    bool found=false;
                    GameObject touchedObject = hit.transform.gameObject;
                    Node n = g.getNode(touchedObject.name);
                    if (n != null)
                    {
                        foreach (Edge e in totalPath)
                        {
                            if (e.from == n || e.to == n) found = true;
                        }
                        if (!found && (!boost || !boostList.Contains(n)) && (!freeze || !freezeList.Contains(n)) && !blockList.Contains(n) && int.Parse(Blocks.text) > 0 &&n!=currentNode)
                        {
                            Blocks.text = "" + (int.Parse(Blocks.text) - 1);
                            OutlineNode(n, startMaterial);
                            g.RemoveNodeConnections(n);
                            blockList.Add(n);
                        }
                        else if (!blockRegeneration && blockList.Contains(n))
                        {

                            Blocks.text = "" + (int.Parse(Blocks.text) + 1);
                            OutlineNode(n, trackMaterial);
                            g.AddNodeConnections(n, matrix, blockList);
                            blockList.Remove(n);
                            Edge lockEdge = new Edge(new Node(-1, -1), n);
                            totalPath.Add(lockEdge);
                        }
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

    private IEnumerator AnimateSolution()
    {
        Edge[] path = null;
        while (!done)
        {
            if (dijkstra)
                path = DijkstraSolver.Solve(g, currentNode, matrix[xEnd, yEnd]);
                
            else
                path = AStarSolver.Solve(g, currentNode, matrix[xEnd, yEnd], myHeuristics[(int)heuristicToUse]);
                
            // check if there is a solution
            if (path.Length == 0)
            {
                EndButton.SetActive(true);
                EndButton.GetComponentInChildren<Text>().text = "Sorry, No solution left\nScore: " + Score.text;
                done = true;
            }
            else
            {
                delay = (startingDelay / acceleration) * (path[0].weight/2 + 3);
                yield return new WaitForSeconds(delay);

                if (!blockList.Contains(path[0].to))
                {
                    Score.text = "" + (int.Parse(Score.text) + 1);
                    totalPath.Add(path[0]);
                    OutlinePath(totalPath.ToArray(), trackMaterial, trackMaterial, npcMaterial);
                    currentNode = path[0].to;
                }

                if (path[0].to == matrix[xEnd, yEnd])
                {
                    EndButton.SetActive(true);
                    EndButton.GetComponentInChildren<Text>().text = "Sorry, End of the Run\nScore: " + Score.text;
                    done = true;
                }
                else if (boostList.Contains(path[0].to))
                {
                    StartCoroutine(ChangeOfSpeedCoroutine(1, 1.6f));
                    boostList.Remove(path[0].to);
                }
                else if (freezeList.Contains(path[0].to))
                {
                    StartCoroutine(ChangeOfSpeedCoroutine(2, 1 / 1.6f));
                    freezeList.Remove(path[0].to);
                }

            }
        }
    }

    public void Restart()
    {
        Scenes.Load("MainMenu");

    }

    protected virtual Node[,] CreateGrid(GameObject o, int x, int y, float gap)
    {
        Node[,] matrix = new Node[x, y];
        for (int i = 0; i < x; i += 1)
        {
            for (int j = 0; j < y; j += 1)
            {
                matrix[i, j] = new Node(i, j, Instantiate(o));
                matrix[i, j].sceneObject.name = ""+i+","+j;
                if (climbing) matrix[i, j].height = (int)Random.Range(1f, 4f);
                else matrix[i, j].height = 1;
                matrix[i, j].sceneObject.transform.position = transform.position + transform.right * gap * (i - ((x - 1) / 2f)) + transform.forward * gap * (j - ((y - 1) / 2f))+ transform.up * -0.65f;
                matrix[i, j].sceneObject.transform.localScale = new Vector3 ( 1,matrix[i, j].height*1.8f,1);
                matrix[i, j].sceneObject.transform.rotation = transform.rotation;
            }
        }
        return matrix;
    }

    protected void CreateGraph(Graph g, Node[,] crossings)
    {
        for (int i = 0; i < crossings.GetLength(0); i += 1)
        {
            for (int j = 0; j < crossings.GetLength(1); j += 1)
            {
                g.AddNode(crossings[i, j]);
                foreach (Edge e in RandomEdges(crossings, i, j, 1))
                    g.AddEdge(e);
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

    protected float Distance(Node from, Node to)
    {
        return to.height - from.height + 3;
    }

    void insertSpecialBlocks(Node[,] matrix, int blockCount, int height, int weight, Material material, List<Node> specialList)
    {
        while (blockCount > 0)
        {
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    if (Random.Range(0f, 1f) < 0.005 && blockCount > 0&&!freezeList.Contains(matrix[i,j])&& !boostList.Contains(matrix[i, j])&&(i!=xStart||j!=yStart)&&(i!=xEnd||j!= yEnd))
                    {
                        bool good = true;
                        foreach (Edge e in g.getConnections(matrix[i, j]))
                            if (freezeList.Contains(e.to) || boostList.Contains(e.to)) good = false;
                        if (good)
                        {
                            matrix[i, j].height = height;
                            matrix[i, j].sceneObject.transform.localScale = new Vector3(1, matrix[i, j].height * 1.8f, 1);
                            blockCount--;
                            matrix[i, j].sceneObject.GetComponent<MeshRenderer>().material = material;
                            specialList.Add(matrix[i, j]);
                            g.changeWeight(matrix[i, j].description, weight);
                        }
                    }
                }

            }
        }
    }

    private IEnumerator ChangeOfSpeedCoroutine(float time, float coefficient)
    {
        startingDelay = startingDelay / coefficient;
        yield return new WaitForSeconds(time);
        startingDelay = startingDelay * coefficient;
    }


    protected void OutlineNode(Node n, Material m)
    {
        if (n != null)
            n.sceneObject.GetComponent<MeshRenderer>().material = m;
    }

    protected void OutlineSet(List<Node> set, Material m)
    {
        if (m == null) return;
        set.Remove(matrix[xEnd, yEnd]);
        foreach (Node n in set)
        {
            if ((!boost || !boostList.Contains(n)) && (!freeze || !freezeList.Contains(n))) n.sceneObject.GetComponent<MeshRenderer>().material = m;
        }
    }
    protected void OutlinePath(Edge[] path, Material sm, Material tm, Material em)
    {
        if (path.Length == 0) return;
        foreach (Edge e in path)
        {
            e.to.sceneObject.GetComponent<MeshRenderer>().material = tm;
        }
        if (path[0].from.sceneObject) path[0].from.sceneObject.GetComponent<MeshRenderer>().material = sm;
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
}