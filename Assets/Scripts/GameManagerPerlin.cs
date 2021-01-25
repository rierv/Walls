using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;
public class GameManagerPerlin : MonoBehaviour
{
    public Terrain terrain;
    public int heightLevels = 3;
    public GameObject EndButton;
    public GameObject Spawner, DisturbingSpawner;
    public Text Score, Blocks;
    Node currentNode = null, previousNode=null;
    public enum Heuristics { Sight, Zero, Euclidian };
    public HeuristicFunction[] myHeuristics = { SightEstimator, ZeroEstimator, EuclideanEstimator };
    public Heuristics heuristicToUse = Heuristics.Sight;
    public int x = 10, y = 10, speed = 5, blocks = 5, RandomSeed = 0;
    [Range(0f, 1f)] public float edgeProbability = 0.75f;
    public float delay = 0.3f, acceleration = 1f, timeElapsed=0;
    public GameObject startMaterial = null, obstacleMaterial = null, endMaterial = null, boostMaterial = null, freezeMaterial = null;
    // what to put on the scene, not really meaningful
    List<Edge> totalPath = new List<Edge>();
    protected Node[,] matrix;
    public Graph g;
    bool done = false, boost = false, freeze = false, start = false,  climbing = false;
    public bool blockRegeneration = true;
    int boostCount = 0, freezeCount = 0;
    private int xStart = 1, yStart = 1, xEnd = 8, yEnd = 8;
    public List<Node> boostList = new List<Node>(), freezeList = new List<Node>(), blockList = new List<Node>();
    List<Node> seenList = new List<Node>(), visited = new List<Node>();
    float startingDelay;
    float[,] heightPerlin;
    TerrainData td;
    static Vector3 terrainSize;
    static Vector2 gridSize;
    bool sawTheEnd = false;
    GameObject myCamera;
    public GameObject pointer;
    Quaternion previousRotation, toRotation;
    Color originaNpcColor;
    Light RobotLight;
    Node lastEndPosition, currEndPosition;
    void Start()
    {
        Cursor.visible = false;
        RandomSeed = (int)System.DateTime.Now.Ticks;
        Random.InitState(RandomSeed);
        lastEndPosition = null;
        currEndPosition = null;
        if (Scenes.getParam("gridLenght") != "")
        {
            x = int.Parse(Scenes.getParam("gridLenght"));
            y = int.Parse(Scenes.getParam("gridHeight"));
            xStart = int.Parse(Scenes.getParam("xStart")) - 1;
            yStart = int.Parse(Scenes.getParam("yStart")) - 1;
            xEnd = int.Parse(Scenes.getParam("xEnd")) - 1;
            yEnd = int.Parse(Scenes.getParam("yEnd")) - 1;
            delay = 1f - float.Parse(Scenes.getParam("speed")) / 10;
            blocks = int.Parse(Scenes.getParam("blocks"));
            boostCount = int.Parse(Scenes.getParam("boost"));
            freezeCount = int.Parse(Scenes.getParam("freeze"));
            heightLevels = int.Parse(Scenes.getParam("DunesHeight"));
            acceleration = float.Parse(Scenes.getParam("acceleration"))/10;
            blockRegeneration = bool.Parse(Scenes.getParam("blockRegeneration"));
            if (bool.Parse(Scenes.getParam("DisturbingF"))) DisturbingSpawner.SetActive(true);
            if (bool.Parse(Scenes.getParam("DecorativeF"))) Spawner.SetActive(true);

            if (heightLevels > 1) climbing = true;
            if (boostCount > 0) boost = true;
            if (freezeCount > 0) freeze = true;
            if (!blockRegeneration) Blocks.text = "" + blocks;
            else Blocks.text = "" + 5;
        }

        terrain.gameObject.SetActive(true);
        td = terrain.terrainData;
        td.size = new Vector3(x, heightLevels-1, y);
        //terrain.transform.position -=  Vector3((x - 1) / 2, 0, (y - 1) / 2);
        terrain.transform.position -= Vector3.right*.3f  + Vector3.forward*.3f ;
        terrain.GetComponent<PerlinTerrain>().Build();
        Vector3 cameraPosition = terrain.transform.position + Vector3.up * Mathf.Max(x, y) * 2f + new Vector3((x - 1) / 2, 0, (y - 1) / 2) + Vector3.right*.5f;
        heightPerlin = terrain.GetComponent<PerlinTerrain>().GetH();

        startingDelay = delay;
        // create a x * y matrix of nodes (and scene objects)
        // edge weight is now the geometric distance (gap)
        matrix = CreateGrid(x, y);
        //matrix = CreateGrid(sceneObject, x, y, gap);
        // create a graph and put random edges inside
        g = new Graph();
        CreateGraph(g, matrix);

        currentNode = matrix[xStart, yStart];
        previousNode = matrix[xStart, yStart];
        visited.Add(currentNode);
        seenList.Add(currentNode);
        //seenGraph.AddNode(currentNode);
        //seenGraph.setConnections(currentNode, g.getConnections(currentNode));

        if (boost) insertSpecialBlocks(matrix, boostCount, 1, 1, boostMaterial, boostList);
        if (freeze) insertSpecialBlocks(matrix, freezeCount, 1 + heightLevels, 1 + heightLevels + 5, freezeMaterial, freezeList);

        
        start = true;
        terrainSize = td.size;
        gridSize = new Vector2(x, y);
        startMaterial = Instantiate(startMaterial);
        startMaterial.transform.position = getNodePosition(matrix[xStart, yStart]) + Vector3.up;
        endMaterial.transform.position = getNodePosition(matrix[xEnd, yEnd]) + Vector3.up;

        
        RobotLight = startMaterial.GetComponentInChildren<Light>();
        originaNpcColor = RobotLight.color;
    }
    
    void Update()
    {
        if (Vector3.Distance(startMaterial.transform.position, endMaterial.transform.position) < 1.5f && endMaterial.GetComponent<CharacterController>().moving)
        {
            endMaterial.GetComponent<Rigidbody>().AddForce(new Vector3((endMaterial.transform.position - startMaterial.transform.position).normalized.x, .5f, (endMaterial.transform.position - startMaterial.transform.position).normalized.z) * 50);
            Score.text = "" + 0;
            StartCoroutine(playerStopMoving());
        }
        Vector3 tempCoord = (startMaterial.transform.position - terrain.gameObject.transform.position);
        Vector3 coord;
        coord.x = tempCoord.x / terrain.terrainData.size.x;
        coord.y = tempCoord.y / terrain.terrainData.size.y;
        coord.z = tempCoord.z / terrain.terrainData.size.z;

        // get the position of the terrain heightmap where this game object is
        int posXInTerrain = Mathf.RoundToInt(coord.x * terrain.terrainData.heightmapResolution);
        int posYInTerrain = Mathf.RoundToInt(coord.z * terrain.terrainData.heightmapResolution);

        startMaterial.transform.position = Vector3.Lerp(getNodePosition(previousNode), getNodePosition(currentNode), (timeElapsed/delay));
        startMaterial.transform.position = new Vector3(startMaterial.transform.position.x, terrain.terrainData.GetHeight(posXInTerrain, posYInTerrain), startMaterial.transform.position.z);

        Spawner.transform.position = startMaterial.transform.position + Vector3.up;
        
        Node nPosition = FindNodeInGraph(endMaterial.transform.position);
        if (nPosition != null && (xEnd != nPosition.x || yEnd != nPosition.y) && !blockList.Contains(nPosition))
        {
            xEnd = nPosition.x;
            yEnd = nPosition.y;
        }
        if (currentNode != null)
        {
            Vector3 aimedPos = getNodePosition(currentNode);
            aimedPos = new Vector3(aimedPos.x, aimedPos.y - 1, aimedPos.z);
            Quaternion targetRotation = Quaternion.LookRotation(startMaterial.transform.position - aimedPos);
            targetRotation = new Quaternion(targetRotation.x, -targetRotation.y, targetRotation.z, -targetRotation.w);
            //startMaterial.transform.rotation = Quaternion.Lerp(startMaterial.transform.rotation, targetRotation, Time.fixedDeltaTime);
        }
        timeElapsed += Time.deltaTime;

        if (start && ((Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Ended) || Input.GetMouseButtonDown(0)))
        {

            StartCoroutine(AnimateSolution());
            if (blockRegeneration) StartCoroutine(BlocksRecovery(10 / blocks));
            start = false;
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
    private IEnumerator playerStopMoving()
    {
        endMaterial.GetComponent<CharacterController>().moving = false;
        yield return new WaitForSeconds(1.5f);
        endMaterial.GetComponent<CharacterController>().moving = true;
    }

    private IEnumerator AnimateSolution()
    {
        Edge[] path = null;
        int count = 0;
        while (!done)
        {
            if (path != null && (count < path.Length || path.Length == 0))
            {
                Debug.Log(currentNode.description);
                if (path.Length == 0)
                {
                    Debug.Log("Lunghezza zero");
                    EndButton.SetActive(true);
                    EndButton.GetComponentInChildren<Text>().text = "Enemy stuck!\nScore: " + Score.text;
                    Cursor.visible = true;
                    done = true;
                }
                else
                {

                    delay = startingDelay * ((path[count].weight - heightLevels + 1) / (1 + acceleration)) / 1.5f;
                    acceleration *= 1.1f;
                    Debug.Log("Nuovo nodo");

                    if (path[count].to == matrix[xEnd, yEnd])
                    {
                        if (Vector3.Distance(startMaterial.transform.position, endMaterial.transform.position) < 3f && endMaterial.GetComponent<CharacterController>().moving)
                        {
                            endMaterial.GetComponent<Rigidbody>().AddForce(new Vector3((endMaterial.transform.position - startMaterial.transform.position).normalized.x, .5f, (endMaterial.transform.position - startMaterial.transform.position).normalized.z) * 50);
                            Score.text = "" + 0;
                            StartCoroutine(playerStopMoving());
                        }
                        yield return new WaitForSeconds(delay);

                        sawTheEnd = true;
                        path = null;
                    }

                    else if (boostList.Contains(path[count].to))
                    {
                        StartCoroutine(ChangeOfSpeedCoroutine(1, 1.6f));
                        boostList.Remove(path[count].to);
                    }
                    else if (freezeList.Contains(path[count].to))
                    {
                        StartCoroutine(ChangeOfSpeedCoroutine(2, 1 / 1.6f));
                        freezeList.Remove(path[count].to);
                    }

                    if (path != null && !blockList.Contains(path[count].to) && path[count].to != matrix[xEnd, yEnd])
                    {

                        totalPath.Add(path[count]);
                        previousNode = currentNode;
                        currentNode = path[count].to;
                        timeElapsed = 0;
                        Debug.Log(isHit(currentNode, matrix[xEnd, yEnd]));
                        nodeDiscover();
                        if (isPlayerOnSight())
                        {
                            Debug.Log("vedo player");

                            RobotLight.color = Color.red;
                            lastEndPosition = matrix[xEnd, yEnd];
                            if (currEndPosition == null || Vector3.Distance(getNodePosition(currEndPosition), getNodePosition(lastEndPosition)) > .07f)
                            {
                                sawTheEnd = true;
                                path = null;
                            }
                        }
                        else if (currEndPosition != null)
                        {
                            path = null;
                        }
                        yield return new WaitForSeconds(delay);

                    }
                    path = checkPath(path);

                    count++;
                }
                Score.text = "" + (int.Parse(Score.text) + 1);

            }
            else if (isPlayerOnSight() || sawTheEnd)
            {

                //removeNodeFromBlockList(currentNode);
                //removeNodeFromBlockList(matrix[xEnd, yEnd]);
                currEndPosition = matrix[xEnd, yEnd];
                lastEndPosition = matrix[xEnd, yEnd];
                sawTheEnd = false;
                Debug.Log("vado a prendere il player in pos:" + currEndPosition.x + " " + currEndPosition.y + " mi trovo in pos " + currentNode.x + " " + currentNode.y);
                path = null;
                if (currentNode.x != currEndPosition.x || currentNode.y != currEndPosition.y) path = AStarSolver.Solve(g, currentNode, currEndPosition, myHeuristics[(int)Heuristics.Sight]);
                else yield return new WaitForSeconds(delay);
                if (path != null) Debug.Log("il percorso è lungo " + path.Length);

                RobotLight.color = Color.red;
                count = 0;
                Debug.DrawRay(getNodePosition(currentNode) + Vector3.up, getNodePosition(matrix[xEnd, yEnd]) - getNodePosition(currentNode), Color.white, 20);
            }
            else
            {
                Debug.Log("vado a prendere un punto a caso o vicino al giocatore (se visto)");

                Node target;
                if (currEndPosition != null) lastEndPosition = matrix[xEnd, yEnd];
                currEndPosition = null;
                //removeNodeFromBlockList(target);
                //removeNodeFromBlockList(currentNode);
                RobotLight.color = originaNpcColor;
                if (currentNode != matrix[xEnd, yEnd])
                {
                    target = bestNodeinSight();
                    if (target != null) path = AStarSolver.Solve(g, currentNode, target, myHeuristics[(int)Heuristics.Sight]);
                }

                else yield return new WaitForSeconds(.1f);
                yield return new WaitForSeconds(.1f);

                count = 0;
            }
        }
    }

    public void Restart()
    {
        Scenes.Load("MainMenu");

    }

    protected virtual Node[,] CreateGrid(int x, int y)
    {
        Node[,] matrix = new Node[x, y];
        for (int i = 0; i < x; i += 1)
        {
            for (int j = 0; j < y; j += 1)
            {
                matrix[i, j] = new Node(i, j);

                matrix[i, j].height = heightPerlin[(int)(j * (heightPerlin.GetLength(1) / y)), (int)(i * (heightPerlin.GetLength(0) / x))]*(heightLevels);

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
        return to.height - from.height + 1 + heightLevels;
    }

    void insertSpecialBlocks(Node[,] matrix, int blockCount, int height, int weight, GameObject go, List<Node> specialList)
    {
        int stop = 0;
        while (blockCount > 0 && stop < 500)
        {
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    if (Random.Range(0f, 1f) < 0.005 && blockCount > 0 && !freezeList.Contains(matrix[i, j]) && !boostList.Contains(matrix[i, j]) && (i != xStart || j != yStart) && (i != xEnd || j != yEnd))
                    {
                        bool good = true;
                        foreach (Edge e in g.getConnections(matrix[i, j]))
                            if (freezeList.Contains(e.to) || boostList.Contains(e.to)) good = false;
                        if (good)
                        {
                            GameObject newgo = Instantiate(go);
                            newgo.transform.position = new Vector3(i * (td.size.x / x), matrix[i, j].height, j * (td.size.z / y));
                            matrix[i, j].height = height;
                            blockCount--;
                            specialList.Add(matrix[i, j]);
                            g.changeWeight(matrix[i, j].description, weight);
                        }
                    }
                }

            }
            stop++;
        }
    }

    private IEnumerator ChangeOfSpeedCoroutine(float time, float coefficient)
    {
        startingDelay = startingDelay / coefficient;
        yield return new WaitForSeconds(time);
        startingDelay = startingDelay * coefficient;
    }

    public void AddNodeConnections(Node n, Node[,] crossings, List<Node> blockList)
    {

        if (n.x > 0 && !blockList.Contains(crossings[n.x - 1, n.y]))
        {
            g.AddEdge(new Edge(crossings[n.x - 1, n.y], n, Distance(crossings[n.x - 1, n.y], crossings[n.x, n.y])));
            g.AddEdge(new Edge(n, crossings[n.x - 1, n.y], Distance(crossings[n.x, n.y], crossings[n.x - 1, n.y])));
        }
        if (n.y > 0 && !blockList.Contains(crossings[n.x, n.y - 1]))
        {
            g.AddEdge(new Edge(crossings[n.x, n.y - 1], n, Distance(crossings[n.x, n.y - 1], crossings[n.x, n.y])));
            g.AddEdge(new Edge(n, crossings[n.x, n.y - 1], Distance(crossings[n.x, n.y], crossings[n.x, n.y - 1])));
        }
        if (n.x < crossings.GetLength(0) - 1 && !blockList.Contains(crossings[n.x + 1, n.y]))
        {
            g.AddEdge(new Edge(crossings[n.x + 1, n.y], n, Distance(crossings[n.x + 1, n.y], crossings[n.x, n.y])));
            g.AddEdge(new Edge(n, crossings[n.x + 1, n.y], Distance(crossings[n.x, n.y], crossings[n.x + 1, n.y])));
        }
        if (n.y < crossings.GetLength(1) - 1 && !blockList.Contains(crossings[n.x, n.y + 1]))
        {
            g.AddEdge(new Edge(crossings[n.x, n.y + 1], n, Distance(crossings[n.x, n.y + 1], crossings[n.x, n.y])));
            g.AddEdge(new Edge(n, crossings[n.x, n.y + 1], Distance(crossings[n.x, n.y], crossings[n.x, n.y + 1])));
        }

    }

    protected static float EuclideanEstimator(Node from, Node to)
    {
        return (getNodePosition(from) - getNodePosition(to)).magnitude;
    }

    protected static float SightEstimator(Node from, Node to) {
        if (isHit(from, to)) return .9f;
        else return .1f;
    }

    protected static float ZeroEstimator(Node from, Node to) { return 0f; }


    public static Vector3 getNodePosition(Node n)
    {
        return new Vector3(n.x * (terrainSize.x / gridSize.x), n.height, n.y * (terrainSize.z / gridSize.y));
    }

    static bool isHit (Node currNode, Node nodeToHit)
    {
        RaycastHit hit;
        if (Physics.Raycast(getNodePosition(currNode) + Vector3.up- Vector3.Normalize(getNodePosition(nodeToHit) - getNodePosition(currNode)), getNodePosition(nodeToHit) - getNodePosition(currNode) - Vector3.up + Vector3.Normalize(getNodePosition(nodeToHit) - getNodePosition(currNode)), out hit, Mathf.Infinity)&& hit.collider != null) {
             if (Vector3.Distance(hit.point, getNodePosition(nodeToHit)) < 1) return true;
        }
        return false;
    }
    Edge [] checkPath(Edge[] path)
    {
        if (path != null)
        {
            foreach (Edge e in path)
            {
                if (blockList.Contains(e.to)) path = null;
            }
        }
        return path;
    }
    bool nodeDiscover()
    {
        bool newNodesFound = false;
        foreach(Node n in g.getNodes())
        {

            if (!seenList.Contains(n) && !blockList.Contains(n) && isHit(currentNode, n))
            {
                seenList.Add(n);
                newNodesFound = true;
            }
        }
        //seenGraph.setConnections();
        return newNodesFound;
    }
    Node bestNodeinSight()
    {
        Node candidate = null;
        int near = 0, minNear = 0;
        float maxDistance = 0;
        if (lastEndPosition != null && lastEndPosition!=currentNode && !blockList.Contains(lastEndPosition))
        {
            if (lastEndPosition != currentNode) candidate = lastEndPosition;
            else
            {
                foreach (Edge e in g.getConnections(lastEndPosition))
                {
                    if (!blockList.Contains(e.to)) candidate = e.to;
                }
            }
        }
        nodeDiscover();
        if (candidate == null)
        { 
                foreach (Node n in seenList)
                {
                    near = 0;

                    if (!visited.Contains(n))
                    {
                        foreach (Edge e in g.getConnections(n))
                            if (!seenList.Contains(e.to)) near++;
                        if (near > minNear && Vector3.Distance(getNodePosition(n), getNodePosition(currentNode)) > maxDistance)
                        {
                            minNear = near;
                            maxDistance = Vector3.Distance(getNodePosition(n), getNodePosition(currentNode));
                            candidate = n;
                        }
                    }
                }
        }
        if (candidate == null) candidate=g.getConnections(currentNode)[0].to;
        return candidate;
    }

    public bool isPlayerOnSight()
    {
        Debug.DrawRay(startMaterial.transform.position , (endMaterial.transform.position - startMaterial.transform.position), Color.red, Mathf.Infinity);
        RaycastHit hit;
        if (Physics.Raycast(startMaterial.transform.position , (endMaterial.transform.position - startMaterial.transform.position), out hit, Mathf.Infinity) && hit.collider != null)
        
            if (hit.collider.gameObject.layer==9) return true;
        
        return false;
    }

    public Node FindNodeInGraph(Vector3 position)
    {
        return g.FindNear(position.x, position.z, position.y, td.size.x / gridSize.x, td.size.z / gridSize.y, xEnd, yEnd);
    }
    public void AddObstacle(Node n)
    {
        if ((!boost || !boostList.Contains(n)) && (!freeze || !freezeList.Contains(n)) && !blockList.Contains(n) && int.Parse(Blocks.text) > 0 && n != currentNode)
        {
            Blocks.text = "" + (int.Parse(Blocks.text) - 1);
            g.RemoveNodeConnections(n);
            blockList.Add(n);
            if (seenList.Contains(n)) seenList.Remove(n);
        }
    }
    public void RemoveObstacle (Node n)
    {
        Blocks.text = "" + (int.Parse(Blocks.text) + 1);
        Destroy(n.sceneObject);
        AddNodeConnections(n, matrix, blockList);
        blockList.Remove(n);
    }
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.layer == 9)
        {
            EndButton.SetActive(true);
            EndButton.GetComponentInChildren<Text>().text = "You fell down!\nScore: " + Score.text;
            done = true;
            Cursor.visible = true;
        }
    }
}

