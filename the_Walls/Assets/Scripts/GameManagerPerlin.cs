﻿using System.Collections;
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
    bool done = false, boost = false, freeze = false, start = false, blockRegeneration = true, climbing = false;
    int boostCount = 0, freezeCount = 0;
    private int xStart = 1, yStart = 1, xEnd = 8, yEnd = 8;
    List<Node> boostList = new List<Node>(), freezeList = new List<Node>(), blockList = new List<Node>(), seenList = new List<Node>();
    float startingDelay;
    float[,] heightPerlin;
    TerrainData td;
    static Vector3 terrainSize;
    static Vector2 gridSize;
    bool sawTheEnd = false;
    bool thirdPersonView = false;
    GameObject myCamera;
    public GameObject pointer;
    Quaternion previousRotation, toRotation;
    Node lastEndPosition, currEndPosition;
    Color originaNpcColor;
    float rotationX=0, rotationY=0;
    void Start()
    {
        if (!Input.gyro.enabled)
        {
            Input.gyro.enabled = true;
        }

        RandomSeed = (int)System.DateTime.Now.Ticks;
        Random.InitState(RandomSeed);
        
        if (Scenes.getParam("gridLenght") != "")
        {
            x = int.Parse(Scenes.getParam("gridLenght"));
            y = int.Parse(Scenes.getParam("gridHeight"));
            xStart = int.Parse(Scenes.getParam("xStart")) - 1;
            yStart = int.Parse(Scenes.getParam("yStart")) - 1;
            xEnd = int.Parse(Scenes.getParam("xEnd")) - 1;
            yEnd = int.Parse(Scenes.getParam("yEnd")) - 1;
            delay = 1f - float.Parse(Scenes.getParam("speed")) / 35;
            blocks = int.Parse(Scenes.getParam("blocks"));
            boostCount = int.Parse(Scenes.getParam("boost"));
            freezeCount = int.Parse(Scenes.getParam("freeze"));
            heightLevels = int.Parse(Scenes.getParam("DunesHeight"));
            acceleration = float.Parse(Scenes.getParam("acceleration"))/10;
            blockRegeneration = bool.Parse(Scenes.getParam("blockRegeneration"));
            thirdPersonView = bool.Parse(Scenes.getParam("thirdPersonView"));
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
        Vector3 cameraPosition = terrain.transform.position + Vector3.up * Mathf.Max(x, y) * 2.3f + new Vector3((x - 1) / 2, 0, (y - 1) / 2) + Vector3.right*.8f;
        heightPerlin = terrain.GetComponent<PerlinTerrain>().GetH();

        startingDelay = delay / 2;
        // create a x * y matrix of nodes (and scene objects)
        // edge weight is now the geometric distance (gap)
        matrix = CreateGrid(x, y);
        //matrix = CreateGrid(sceneObject, x, y, gap);
        // create a graph and put random edges inside
        g = new Graph();
        CreateGraph(g, matrix);
        originaNpcColor = startMaterial.GetComponent<MeshRenderer>().sharedMaterial.color;

        currentNode = matrix[xStart, yStart];
        previousNode = matrix[xStart, yStart];
        seenList.Add(currentNode);
        //seenGraph.AddNode(currentNode);
        //seenGraph.setConnections(currentNode, g.getConnections(currentNode));

        if (boost) insertSpecialBlocks(matrix, boostCount, 1, 1, boostMaterial, boostList);
        if (freeze) insertSpecialBlocks(matrix, freezeCount, 1 + heightLevels, 1 + heightLevels + 5, freezeMaterial, freezeList);

        myCamera = GameObject.Find("Main Camera");
        myCamera.transform.position = cameraPosition;
        start = true;
        terrainSize = td.size;
        gridSize = new Vector2(x, y);
        startMaterial = Instantiate(startMaterial);
        startMaterial.transform.position = getNodePosition(matrix[xStart, yStart]) + Vector3.up*.5f;
        endMaterial = Instantiate(endMaterial);
        endMaterial.transform.position = getNodePosition(matrix[xEnd, yEnd]);

        if (thirdPersonView)
        {
            pointer.transform.position = endMaterial.transform.position + Vector3.up;
            myCamera.transform.position = endMaterial.transform.position + Vector3.up;
            pointer.transform.LookAt(new Vector3(startMaterial.transform.position.x, endMaterial.transform.position.y, startMaterial.transform.position.z));
        }
        
        lastEndPosition = null;
        currEndPosition = null;
    }

    void FixedUpdate()
    {
        startMaterial.transform.position = Vector3.Lerp(getNodePosition(previousNode) + Vector3.up * .5f, getNodePosition(currentNode)+ Vector3.up*.5f, (timeElapsed)/(delay));
        Spawner.transform.position = startMaterial.transform.position + Vector3.up;
        Node nPosition = g.FindNear(endMaterial.transform.position.x, endMaterial.transform.position.z, endMaterial.transform.position.y, td.size.x / x, td.size.z / y, xEnd, yEnd);
        if (xEnd != nPosition.x || yEnd != nPosition.y)
        {
            xEnd = nPosition.x;
            yEnd = nPosition.y;
        }
        if (Input.gyro.userAcceleration.magnitude > .01f)
        {
            rotationX = Mathf.Clamp( rotationX - Input.gyro.rotationRateUnbiased.x*50 , -1, 1) ;
            rotationY = Mathf.Clamp(rotationY - Input.gyro.rotationRateUnbiased.y*50, -1, 1) ;
        }
        if (thirdPersonView) {
            
            myCamera.transform.rotation= Quaternion.Lerp(myCamera.transform.rotation, pointer.transform.rotation, .4f);
            myCamera.transform.position = endMaterial.transform.position + Vector3.up - myCamera.transform.forward *3 ;
            if (Input.GetKey(KeyCode.Space)) endMaterial.transform.Translate((getNodePosition(matrix[xEnd, yEnd]) + myCamera.transform.forward - endMaterial.transform.position )*Time.fixedDeltaTime/2);
            
            if (Input.GetKey(KeyCode.LeftArrow)) {
                pointer.transform.Rotate(0, -1f,0);
            }
            if (Input.GetKey(KeyCode.RightArrow)) {
                pointer.transform.Rotate(0, 1f, 0);
            }
            if (Input.gyro.attitude != Quaternion.identity)
            {
                //endMaterial.transform.position = Vector3.Lerp(endMaterial.transform.position, getNodePosition(matrix[xEnd, yEnd]) + myCamera.transform.forward * Mathf.Clamp(Input.gyro.attitude.y*5, -3, 3), .1f );
                endMaterial.transform.Translate((getNodePosition(matrix[xEnd, yEnd]) + myCamera.transform.forward * rotationX*5 - endMaterial.transform.position) * Time.fixedDeltaTime);

                pointer.transform.Rotate(Vector3.up * rotationY);
            }

        }
        else
        {
            if (Input.gyro.attitude != Quaternion.identity)
                endMaterial.transform.Translate((getNodePosition(matrix[xEnd, yEnd]) + new Vector3(rotationX, 0, rotationY) - endMaterial.transform.position) * Time.fixedDeltaTime);
                //endMaterial.transform.position = Vector3.Lerp(endMaterial.transform.position, getNodePosition(matrix[xEnd, yEnd]) + new Vector3(Mathf.Clamp(Input.gyro.attitude.x*5, -3, 3), 0, Mathf.Clamp(Input.gyro.attitude.y*5, -3, 3)), .1f);

            if (Input.GetKey(KeyCode.LeftArrow))
            {
                endMaterial.transform.position = Vector3.Lerp(endMaterial.transform.position, getNodePosition(matrix[xEnd, yEnd]) - Vector3.right, .05f);
            }
            if (Input.GetKey(KeyCode.RightArrow))
            {
                endMaterial.transform.position = Vector3.Lerp(endMaterial.transform.position, getNodePosition(matrix[xEnd, yEnd]) + Vector3.right, .05f);
            }
            if (Input.GetKey(KeyCode.UpArrow))
            {
                endMaterial.transform.position = Vector3.Lerp(endMaterial.transform.position, getNodePosition(matrix[xEnd, yEnd]) + Vector3.forward, .05f);
            }
            if (Input.GetKey(KeyCode.DownArrow))
            {
                endMaterial.transform.position = Vector3.Lerp(endMaterial.transform.position, getNodePosition(matrix[xEnd, yEnd]) - Vector3.forward, .05f);
            }
        }

        timeElapsed += Time.fixedDeltaTime;

        if (start && ((Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Ended) || Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space)))
        {

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
                    GameObject touchedObject = hit.transform.gameObject;
                    if (touchedObject.name == "Terrain")
                    {
                        Node n = g.FindNear(hit.point.x, hit.point.z, hit.point.y, td.size.x/x, td.size.z/y, xEnd, yEnd);
                        if (n != null)
                        {

                            if ((!boost || !boostList.Contains(n)) && (!freeze || !freezeList.Contains(n)) && !blockList.Contains(n) && int.Parse(Blocks.text) > 0 && n != currentNode)
                            {
                                Blocks.text = "" + (int.Parse(Blocks.text) - 1);
                                GameObject newgo = Instantiate(obstacleMaterial);
                                newgo.transform.position = new Vector3(n.x * (td.size.x / x), n.height + 1, n.y * (td.size.z / y));
                                n.sceneObject = newgo;
                                g.RemoveNodeConnections(n);
                                blockList.Add(n);
                                if (seenList.Contains(n)) seenList.Remove(n);
                            }
                            else if (!blockRegeneration && blockList.Contains(n))
                            {
                                Blocks.text = "" + (int.Parse(Blocks.text) + 1);
                                Destroy(n.sceneObject);
                                AddNodeConnections(n, matrix, blockList);
                                blockList.Remove(n);
                            }
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
        int count = 0;
        while (!done)
        {
            if (path != null && (count<path.Length|| path.Length==0))
            {
                if (path.Length == 0)
                {
                    EndButton.SetActive(true);
                    EndButton.GetComponentInChildren<Text>().text = "Sorry, No solution left\nScore: " + Score.text;
                    done = true;
                }
                else
                {
                    delay = ((startingDelay + ((path[count].weight - heightLevels + 1) / 2)) / (1 + acceleration))/1.5f;
                    acceleration *= 1.1f;
                    
                    if (path[count].to == matrix[xEnd, yEnd])
                    {
                        EndButton.SetActive(true);
                        EndButton.GetComponentInChildren<Text>().text = "Sorry, End of the Run\nScore: " + Score.text;
                        done = true;
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

                    if (!blockList.Contains(path[count].to))
                    {
                        Score.text = "" + (int.Parse(Score.text) + 1);
                        totalPath.Add(path[count]);
                        previousNode = currentNode;
                        currentNode = path[count].to;
                        timeElapsed = 0;
                        Debug.Log(isHit(currentNode, matrix[xEnd, yEnd]));
                        nodeDiscover();
                        if (isHit(currentNode, matrix[xEnd, yEnd]))
                        {
                            lastEndPosition = matrix[xEnd, yEnd];
                            if (currEndPosition == null || Vector3.Distance(getNodePosition(currEndPosition), getNodePosition(lastEndPosition))>2)
                            {
                                sawTheEnd = true;
                                path = null;
                            }
                        }
                        else if (currEndPosition!= null)
                        {
                            path = null;
                        }
                        yield return new WaitForSeconds(delay);

                    }
                    path = checkPath(path);
                    
                    count++;
                }
            }
            else if (isHit(currentNode, matrix[xEnd, yEnd])||sawTheEnd)
            {
                currEndPosition = matrix[xEnd, yEnd];
                lastEndPosition = matrix[xEnd, yEnd];
                sawTheEnd = false;
                path = AStarSolver.Solve(g, currentNode, currEndPosition, myHeuristics[(int)Heuristics.Sight]);
                startMaterial.GetComponent<MeshRenderer>().material.color = Color.yellow;
                count = 0;
                Debug.DrawRay(getNodePosition(currentNode) +Vector3.up - Vector3.Normalize(getNodePosition(matrix[xEnd, yEnd]) - getNodePosition(currentNode)), getNodePosition(matrix[xEnd, yEnd]) - getNodePosition(currentNode) +Vector3.Normalize(getNodePosition(matrix[xEnd, yEnd]) - getNodePosition(currentNode)) -Vector3.up, Color.white, 10);
            }
            else
            {
                currEndPosition = null;
                startMaterial.GetComponent<MeshRenderer>().material.color = originaNpcColor;
                path = AStarSolver.Solve(g, currentNode, bestNodeinSight(), myHeuristics[(int)Heuristics.Sight]);
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


    static Vector3 getNodePosition(Node n)
    {
        return new Vector3(n.x * Mathf.Floor((terrainSize.x / gridSize.x)), n.height, n.y * Mathf.Floor((terrainSize.z / gridSize.y)));
    }

    static bool isHit (Node currNode, Node nodeToHit)
    {
        RaycastHit hit;
        if (Physics.Raycast(getNodePosition(currNode) + Vector3.up - Vector3.Normalize(getNodePosition(nodeToHit) - getNodePosition(currNode)*2), getNodePosition(nodeToHit) - getNodePosition(currNode) - Vector3.up + Vector3.Normalize(getNodePosition(nodeToHit) - getNodePosition(currNode)*2), out hit, Mathf.Infinity)&& hit.collider != null) {
             if (Vector3.Distance(hit.point, getNodePosition(nodeToHit)) < 2f && hit.normal.y > -.25f) return true;
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
            //Debug.DrawRay(getNodePosition(currentNode) + Vector3.up, getNodePosition(n) - getNodePosition(currentNode), Color.white, 10);

            if (!blockList.Contains(n) && isHit(currentNode, n))
            {
                seenList.Add(n);
                newNodesFound = true;
            }
            else seenList.Remove(n);
        }
        //seenGraph.setConnections();
        return newNodesFound;
    }
    Node bestNodeinSight()
    {
        Node candidate=null;
        nodeDiscover();
        int near = 0, minNear=0;
        float maxDistance=0;

        if (currentNode == lastEndPosition) lastEndPosition = null;
        else if (lastEndPosition!=null) return lastEndPosition;
        foreach (Node n in seenList)
        {
            near = 0;
            foreach (Edge e in g.getConnections(n))
                if (!seenList.Contains(e.to)) near++;
            if (near > minNear && Vector3.Distance(getNodePosition(n), getNodePosition(currentNode)) > maxDistance)
            {
                minNear = near;
                maxDistance = Vector3.Distance(getNodePosition(n), getNodePosition(currentNode));
                candidate = n;
            }
        }
        if (candidate == null) candidate=g.getConnections(currentNode)[0].to;
        return candidate;
    }
}