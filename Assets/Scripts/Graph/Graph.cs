
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class Graph {

    // holds all edgeds going out from a node
    private Dictionary<Node, List<Edge>> data;

    public Graph() {
        data = new Dictionary<Node, List<Edge>>();
    }

    public void AddEdge(Edge e) {
        AddNode(e.from);
        AddNode(e.to);
        if (!data[e.from].Contains(e))
        {
            data[e.from].Add(e);
            e = new Edge(e.to, e.from);
            if (!data[e.from].Contains(e)) data[e.to].Add(e);
        }
    }

    public void RemoveNodeConnections(Node n)
    {
        if (data.ContainsKey(n))
        {
            foreach (Edge e in getConnections(n))
            {
                foreach (Edge f in getConnections(e.to))
                {
                    if (f.to == n)
                    {
                        if (data[e.to].Contains(f)) data[e.to].Remove(f);
                    }
                }
                if (data[n].Contains(e)) data[n].Remove(e);
            }
            //data.Remove(n);
        }
    }
    public void AddNodeConnections(Node n, Node[,] crossings, List<Node> blockList)
    {
        if (data.ContainsKey(n))
        {
            if (n.x > 0 && !blockList.Contains(crossings[n.x - 1, n.y])) {
                AddEdge(new Edge(crossings[n.x - 1, n.y], n, Distance(crossings[n.x-1, n.y], crossings[n.x, n.y])));
                AddEdge(new Edge(n, crossings[n.x - 1, n.y], Distance(crossings[n.x, n.y], crossings[n.x-1, n.y])));
            }
            if (n.y > 0 && !blockList.Contains(crossings[n.x, n.y - 1]))
            {
                AddEdge(new Edge(crossings[n.x, n.y - 1], n, Distance(crossings[n.x, n.y-1], crossings[n.x, n.y])));
                AddEdge(new Edge(n, crossings[n.x, n.y - 1], Distance(crossings[n.x, n.y], crossings[n.x, n.y - 1])));
            }
            if (n.x < crossings.GetLength(0) - 1 && !blockList.Contains(crossings[n.x + 1, n.y]))
            {
                AddEdge(new Edge(crossings[n.x + 1, n.y], n, Distance(crossings[n.x+1, n.y], crossings[n.x, n.y])));
                AddEdge(new Edge(n, crossings[n.x + 1, n.y], Distance(crossings[n.x, n.y], crossings[n.x+1, n.y])));
            }
            if (n.y < crossings.GetLength(1) - 1 && !blockList.Contains(crossings[n.x, n.y + 1]))
            {
                AddEdge(new Edge(crossings[n.x, n.y + 1], n, Distance(crossings[n.x, n.y+1], crossings[n.x, n.y])));
                AddEdge(new Edge(n, crossings[n.x, n.y + 1], Distance(crossings[n.x, n.y], crossings[n.x, n.y + 1])));
            }

            //data.Remove(n);
        }
    }


    protected float Distance(Node from, Node to)
    {
        return to.height - from.height + 1;
    }

    public Edge EdgeTo (Node from, Node to)
    {
        foreach (Edge e in data[from])
            if (e.to == to) return e;
        return null;
    } 
	// used only by AddEdge 
	public void AddNode(Node n) {
		if (!data.ContainsKey (n))
			data.Add (n, new List<Edge> ());
	}

	// returns the list of edged exiting from a node
	public Edge[] getConnections(Node n) {
		if (!data.ContainsKey (n)) return new Edge[0];
		return data [n].ToArray ();
	}
    public void changeWeight(String n, float newWeight)
    {
        foreach (Edge e in data[getNode(n)])
        {
            e.weight = newWeight;
        }
        foreach (Node node in getNodes())
            foreach (Edge e in data[node])
                if(e.to.description == n) e.weight = newWeight;
        return;
    }

    public Node getNode(String Description)
    {
        foreach(Node n in data.Keys)
        {
            if (n.description == Description) return n;
        }
        return null;
    }

	public Node[] getNodes() {
		return data.Keys.ToArray ();
	}

}