using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
 
public class FlyCamera : MonoBehaviour {
 
    /* Originally Written by Windexglow 11-13-10. */
    // Modified by Jonas De Maeseneer
    
    float _mainSpeed = 40.0f; //regular speed
    float _shiftAdd = 60.0f; //multiplied by how long shift is held.  Basically running
    float _maxShift = 100.0f; //Maximum speed when holdin gshift
    float _camSens = 0.11f; //How sensitive it with mouse
    private Vector3 _lastMouse = new Vector3(255, 255, 255); //kind of in the middle of the screen, rather than at the top (play)
    private float _totalRun= 1.0f;
    
    public Vector3 Max = new Vector3(10000, 10000, 10000);
    public Vector3 Min = new Vector3(-10000, -10000, -10000);
    
    [SerializeField]
    private List<RectTransform> _rectTransformsToIgnoreMouseOn = new List<RectTransform>();
    private bool _pressedOnValidArea = false;

    void Update () {

        if (Input.GetMouseButtonDown(0))
        {
            _lastMouse = Input.mousePosition;

            _pressedOnValidArea = !IsOnBlockedRect();
        }
        
        if (Input.GetMouseButton(0) && _pressedOnValidArea)
        {
            //Mouse camera angle
            _lastMouse = Input.mousePosition - _lastMouse ;
            _lastMouse = new Vector3(-_lastMouse.y * _camSens, _lastMouse.x * _camSens, 0 );
            _lastMouse = new Vector3(transform.eulerAngles.x + _lastMouse.x , transform.eulerAngles.y + _lastMouse.y, 0);
            transform.eulerAngles = _lastMouse;
            _lastMouse =  Input.mousePosition;
        }
       
        //Keyboard commands
        Vector3 p = GetBaseInput();
        if (p.sqrMagnitude > 0){ // only move while a direction key is pressed
          if (Input.GetKey (KeyCode.LeftShift)){
              _totalRun += Time.deltaTime;
              p *= (_totalRun * _shiftAdd);
              p.x = Mathf.Clamp(p.x, -_maxShift, _maxShift);
              p.y = Mathf.Clamp(p.y, -_maxShift, _maxShift);
              p.z = Mathf.Clamp(p.z, -_maxShift, _maxShift);
          } else {
              _totalRun = Mathf.Clamp(_totalRun * 0.5f, 1f, 1000f);
              p *= _mainSpeed;
          }
         
          p *= Time.deltaTime;
          float3 newPos = transform.position + (Vector3)(transform.localToWorldMatrix * p);
          newPos = math.clamp(newPos, Min, Max);
          transform.position = newPos;
        }
    }
     
    private Vector3 GetBaseInput() { //returns the basic values, if it's 0 than it's not active.
        Vector3 pVelocity = new Vector3();
        if (Input.GetKey (KeyCode.W)){
            pVelocity += new Vector3(0, 0 , 1);
        }
        if (Input.GetKey (KeyCode.S)){
            pVelocity += new Vector3(0, 0, -1);
        }
        if (Input.GetKey (KeyCode.A)){
            pVelocity += new Vector3(-1, 0, 0);
        }
        if (Input.GetKey (KeyCode.D)){
            pVelocity += new Vector3(1, 0, 0);
        }
        return pVelocity;
    }

    private bool IsOnBlockedRect()
    {
        foreach (var rectTransform in _rectTransformsToIgnoreMouseOn)
        {
            Vector2 localMousePosition = rectTransform.InverseTransformPoint(Input.mousePosition);
            if (rectTransform.rect.Contains(localMousePosition))
            {
                return true;
            }
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Bounds bounds = new Bounds();
        bounds.SetMinMax(Min,Max);
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}