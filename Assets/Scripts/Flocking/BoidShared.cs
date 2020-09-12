using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoidShared : MonoBehaviour {

	[Range(0f, 10f)] public float _BoidFOV = 2f;
	public static float BoidFOW = 0f;

	[Range (1f, 20f)] public float _BoidSpeed = 10f;
	public static float BoidSpeed = 0f;

	[Range (0f, 1f)] public float _AlignComponent = 1f;
	public static float AlignComponent = 0f;

	[Range (.8f, 1f)] public float _CohesionComponent = 1f;
	public static float CohesionComponent = 0f;

	[Range (.8f, 1f)] public float _SeparationComponent = 1f;
	public static float SeparationComponent = 0f;

	public bool breath = false;
	[Range (0f, .2f)] public float amplitude = .1f;
	[Range (1f, 10f)] public float speed = 1f;

    bool done = true;

	private void Start () {
		OnValidate ();
	}

	private void OnValidate () {
		BoidFOW = _BoidFOV;
		BoidSpeed = _BoidSpeed;
		AlignComponent = _AlignComponent;
		CohesionComponent = _CohesionComponent;
		SeparationComponent = _SeparationComponent;
	}

    private void Update()
    {
        if (breath)
        {
            float c = 1f - ((Mathf.Cos(Time.realtimeSinceStartup * speed) + 1) * amplitude / 2f);
            float s = 1f - ((Mathf.Sin(Time.realtimeSinceStartup * speed) + 1) * amplitude / 2f);
            CohesionComponent = _CohesionComponent = c;
            SeparationComponent = _SeparationComponent = s;
        }

        if (Input.touchCount == 1 && Input.GetTouch(0).phase == 0)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.GetTouch(0).position);
            RaycastHit hit;
            Debug.DrawRay(ray.origin, ray.direction * 100, Color.yellow, 100f);
            if (Physics.Raycast(ray, out hit))
            {
                if ( hit.collider != null&&hit.collider.gameObject==this.gameObject  && done)
                {
                    done = false;
                    StartCoroutine(Touched());
                }
            }
        }
    }
    private IEnumerator Touched()
    {
        
        GetComponent<BoidSpawner>().UpdateRadius(30);
        BoidSpeed *= 4;
        CohesionComponent /= 4;
        SeparationComponent *= 4;
        yield return new WaitForSeconds(.6f);
        GetComponent<BoidSpawner>().UpdateRadius(GetComponent<BoidSpawner>().radius);
        yield return new WaitForSeconds(.6f);
        BoidSpeed /= 4;
        CohesionComponent *= 4;
        SeparationComponent /= 4;
        done = true;
        
    }

}
