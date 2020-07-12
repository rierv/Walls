
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
		AddNode (e.from);
		AddNode (e.to);
        if (!data[e.from].Contains(e))
        {
            data[e.from].Add(e);
            e = new Edge(e.to, e.from);
            data[e.to].Add(e);
        }
    }

    public void RemoveNode(Node n)
    {
        if (data.ContainsKey(n))
        {
            foreach(Edge e in getConnections(n))
            {
                foreach (Edge f in getConnections(e.to))
                {
                    if (f.to == n)
                    {
                        data[e.to].Remove(f);
                    }
                }
            }
            data.Remove(n);
        }
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