using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Vehicle : MonoBehaviour {

    public Transform[] path;
    private int pathNo = 0;


    void Start()
    {
        MoveToPath();
    }

    void MoveToPath()
    {
        // 移動する距離によって動く時間を計算
        float moveTime = Vector3.Distance(transform.position, path[pathNo].position) / 2;
        iTween.MoveTo(gameObject, iTween.Hash("position", path[pathNo], "time", moveTime, "easetype", "linear", "oncomplete", "MoveToPath", "Looktarget", path[pathNo].position, "looktime", 2.0f));
        pathNo++;
        if (pathNo > path.Length - 1)
        {
            pathNo = 0;
        }
    }

    // SceneビューにGizmo表示
    void OnDrawGizmos()
    {
        iTween.DrawPath(path);
    }
}
