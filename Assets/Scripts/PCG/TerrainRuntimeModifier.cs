using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainRuntimeModifier : MonoBehaviour
{
    Terrain terr; // terrain to modify
    int hmWidth; // heightmap width
    int hmHeight; // heightmap height

    int posXInTerrain; // position of the game object in terrain width (x axis)
    int posYInTerrain; // position of the game object in terrain height (z axis)

    int size = 25; // the diameter of terrain portion that will raise under the game object
    float[,] startingHeights;

    bool firstTouch = true, ready = true;
    public GameManagerPerlin gmp;
    public GameObject placementIndicator;
    int circleRadius = 12;
    public float paintWeight = 0.001f;
    public float rayTimeInterval = 0.1f;
    public float scaleFactor = 1f;
    public float maxHight = .1f;
    Vector2 userInput = Vector2.up;
    private float rayTimer = 0;
    private bool placementPoseIsValid = false;
    private TerrainData terrainData;
    private Vector3 terrainSize;
    Node currentNode;
    public Camera myCamera;
    List<Node> editingList = new List<Node>(), toRemove = new List<Node>();

    void Start()
    {
        placementIndicator = Instantiate(placementIndicator);
        placementIndicator.SetActive(false);
        Debug.Log(placementIndicator.active);

        ready = true;
        terr = Terrain.activeTerrain;
        hmWidth = terr.terrainData.heightmapResolution;
        hmHeight = terr.terrainData.heightmapResolution;
        terrainData = terr.terrainData;
        terrainSize = terr.terrainData.size;
        startingHeights = terr.GetComponent<PerlinTerrain>().GetH();
        myCamera = Camera.main;
        size = 2500 / (int)(terr.terrainData.size.x * terr.terrainData.size.z);

    }

    void Update()
    {
        Debug.Log(placementIndicator.active);
        if (Input.GetMouseButton(0))
        {
            UpdatePlacementPose();
            UpdatePlacementIndicator();
        }
        else placementIndicator.SetActive(false);
        if (placementPoseIsValid && (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)|| Input.GetMouseButtonUp(0))
        {
            if (currentNode != null)
            {
                editingList.Add(currentNode);
                if (!gmp.blockList.Contains(currentNode)) PlaceObject(currentNode);
                else if (!gmp.blockRegeneration) RemoveObject(currentNode);
            }
        }
        EditCircle();
    }
    
     

    void EditCircle()
    {
        int offset = size / 2;
        circleRadius = offset;
        float[,] heights = null;
        foreach(Node n in editingList)
        {
            Vector3 pos = GameManagerPerlin.getNodePosition(n);
            if (gmp.blockList.Contains(n))
            {
                Vector3 tempCoord = (pos - terr.gameObject.transform.position);
                Vector3 coord;
                coord.x = tempCoord.x / terr.terrainData.size.x;
                coord.y = tempCoord.y / terr.terrainData.size.y;
                coord.z = tempCoord.z / terr.terrainData.size.z;

                // get the position of the terrain heightmap where this game object is
                posXInTerrain = Mathf.Max(offset,Mathf.RoundToInt(coord.x * hmWidth));
                posYInTerrain = Mathf.Max(offset, Mathf.RoundToInt(coord.z * hmHeight));

                if (startingHeights == null) startingHeights = terr.GetComponent<PerlinTerrain>().GetH();

                heights = terr.terrainData.GetHeights(posXInTerrain - offset, posYInTerrain - offset, size, size);

                for (int i = -circleRadius; i < circleRadius; i++)
                    for (int j = -circleRadius; j < circleRadius; j++)
                    {
                        if (heights[i + circleRadius, j + circleRadius] < startingHeights[posYInTerrain + i, posXInTerrain + j] + maxHight)
                            heights[i + circleRadius, j + circleRadius] += paintWeight;
                    }
                terrainData.SetHeights(posXInTerrain - offset, posYInTerrain - offset, heights);
            }
            else
            {
                Vector3 tempCoord = (pos - terr.gameObject.transform.position);
                Vector3 coord;
                coord.x = tempCoord.x / terr.terrainData.size.x;
                coord.y = tempCoord.y / terr.terrainData.size.y;
                coord.z = tempCoord.z / terr.terrainData.size.z;

                // get the position of the terrain heightmap where this game object is
                posXInTerrain = Mathf.Max(offset, Mathf.RoundToInt(coord.x * hmWidth));
                posYInTerrain = Mathf.Max(offset, Mathf.RoundToInt(coord.z * hmHeight));

                if (startingHeights == null) startingHeights = terr.GetComponent<PerlinTerrain>().GetH();

                heights = terr.terrainData.GetHeights(posXInTerrain - offset, posYInTerrain - offset, size, size);

                for (int i = -circleRadius; i < circleRadius; i++)
                    for (int j = -circleRadius; j < circleRadius; j++)
                    {
                        if (heights[i + circleRadius, j + circleRadius] > startingHeights[posYInTerrain + i, posXInTerrain + j])
                            heights[i + circleRadius, j + circleRadius] -= paintWeight;
                    }

                terr.terrainData.SetHeights(posXInTerrain - offset, posYInTerrain - offset, heights);

            }
        
        }
        foreach(Node n in toRemove)
        {
            //editingList.Remove(n);
        }
        toRemove.Clear();

    }
        
     

    IEnumerator firstTouchToFalse()
    {
        ready = false;
        yield return new WaitForSeconds(1f);
        ready = true;
        placementIndicator.SetActive(true);
        firstTouch = false;
    }

    private void PlaceObject(Node n)
    {
        
        //Instantiate(objectToPlace, placementPose.position -Vector3.right/3f - Vector3.up/3f - Vector3.forward / 3f, Quaternion.identity);
        //GameOrigin.transform.SetPositionAndRotation(placementPose.position - myCamera.transform.right / 2 - myCamera.transform.up / 2, placementPose.rotation);
        placementIndicator.SetActive(false);
        //StartCoroutine(firstTouchToFalse());
        gmp.AddObstacle(n);
    }
    void RemoveObject(Node n)
    {
        //StartCoroutine(firstTouchToFalse());
        gmp.RemoveObstacle(n);
    }

    private void UpdatePlacementIndicator()
    {
        if (placementPoseIsValid)
        {
            placementIndicator.SetActive(true);
            Vector3 tempCoord = (GameManagerPerlin.getNodePosition(currentNode) - terr.gameObject.transform.position - Vector3.one*.3f);
            Vector3 coord;
            coord.x = tempCoord.x / terr.terrainData.size.x;
            coord.y = tempCoord.y / terr.terrainData.size.y;
            coord.z = tempCoord.z / terr.terrainData.size.z;

            // get the position of the terrain heightmap where this game object is
            int posXInTerrain = Mathf.RoundToInt(coord.x * terr.terrainData.heightmapResolution);
            int posYInTerrain = Mathf.RoundToInt(coord.z * terr.terrainData.heightmapResolution);

            placementIndicator.transform.position = new Vector3(tempCoord.x, terr.terrainData.GetHeight(posXInTerrain, posYInTerrain), tempCoord.z);

            Debug.Log(placementIndicator.transform.position);

        }
        else placementIndicator.SetActive(false);

    }

    private void UpdatePlacementPose()
    {

        Ray screenCenter = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Debug.DrawRay(screenCenter.origin, screenCenter.direction, Color.red, Mathf.Infinity);
        Debug.Log("allora" + Camera.main.transform.position + " " + screenCenter.origin + "  " + screenCenter.direction);
        placementPoseIsValid = false;

        if (Physics.Raycast(screenCenter,out RaycastHit hit, 100) && hit.collider != null)
        {

            if (hit.collider.gameObject.layer == 8)
            {
                Debug.Log("CIAMBALAAA");

                placementPoseIsValid = true;
                currentNode = gmp.FindNodeInGraph(hit.point);
            }
            
        }
        

    }
}