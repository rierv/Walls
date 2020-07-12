
using UnityEngine;

public class Node {

	public string description;
    public int x, y;
	public GameObject sceneObject;

	public Node(int _x, int _y, GameObject o = null) {
        this.x = _x;
        this.y = _y;
		this.description = ""+x+","+y;
		this.sceneObject = o;
	}
}