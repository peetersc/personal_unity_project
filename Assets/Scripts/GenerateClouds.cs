using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class CloudData
{
    public Vector3 pos;
    public Vector3 scale;
    public Quaternion rot;
    private bool _isActive;

    // prevents other classes from directly setting active state
    public bool isActive
    {
        get
        {
            return _isActive;
        }
    }

    public int x;
    public int y;
    public float distFromCam;

    public Matrix4x4 matrix
    {
        get
        {
            return Matrix4x4.TRS(pos, rot, scale);
        }
    }

    public CloudData(Vector3 pos, Vector3 scale, Quaternion rot, int x, int y, float distFromCam)
    {
        this.pos = pos;
        this.scale = scale;
        this.rot = rot;
        SetActive(true);
        this.x = x;
        this.y = y;
        this.distFromCam = distFromCam;
    }

    public void SetActive(bool state)
    {
        _isActive = state;
    }
}

public class GenerateClouds : MonoBehaviour
{
    //public fields
    public Mesh cloudMesh;
    public Material cloudMat;
    public float cloudSize = 5;
    public float maxScale = 1;
    public float timeScale = 1;
    public float texScale = 1;
    public float minNoiseSize = 0.5f;
    public float sizeScale = 0.25f;
    public Camera cam;
    public int maxDist;
    public int batchesToCreate;

    //private fields
    private Vector3 prevCamPos;
    private float offsetX = 1;
    private float offsetY = 1;
    private List<List<CloudData>> batches = new List<List<CloudData>>();
    private List<List<CloudData>> batchesToUpdate = new List<List<CloudData>>();

    private void Start()
    {
        for (int batchesX = 0; batchesX < batchesToCreate; ++batchesX)
        {
            for (int batchesY = 0; batchesY < batchesToCreate; ++batchesY)
            {
                BuildCloudBatch(batchesX, batchesY);
            }
        }
    }

    //limited to 31x31 clouds due to 1024 max of Graphics.DrawMeshInsaciated
    private void BuildCloudBatch(int xLoop, int yLoop)
    {
        bool markBatch = false; // set to ture when in camera range
        List<CloudData> currBatch = new List<CloudData>();

        for (int x = 0; x < 31; ++x)
        {
            for (int y = 0; y < 31; ++y)
            {
                AddCloud(currBatch, x + xLoop * 31, y + yLoop * 31);
            }
        }

        markBatch = CheckForActiveBatch(currBatch); // check if should be marked
        batches.Add(currBatch); // add newest batch to list
        if (markBatch) batchesToUpdate.Add(currBatch); // if marked update
    }

    //checks for cloud that is within camera range
    //return true in range
    //else false
    private bool CheckForActiveBatch(List<CloudData> batch)
    {
        foreach (var cloud in batch)
        {
            cloud.distFromCam = Vector3.Distance(cloud.pos, cam.transform.position);
            if (cloud.distFromCam < maxDist) return true;
        }
        return false;
    }

    private void AddCloud(List<CloudData> currBatch, int x, int y)
    {
        // set new clouds position
        Vector3 position = new Vector3(transform.position.x + x * cloudSize, 
                                       transform.position.y, 
                                       transform.position.z + y * cloudSize);

        //set new clouds distance to the camera
        float disToCam = Vector3.Distance(new Vector3(x, transform.position.y, y), cam.transform.position);

        currBatch.Add(new CloudData(position, Vector3.zero, Quaternion.identity, x, y, disToCam));
    }

    private void Update()
    {
        MakeNoise();
        offsetX += Time.deltaTime * timeScale;
        offsetY += Time.deltaTime * timeScale;
    }

    void MakeNoise()
    {
        if (cam.transform.position == prevCamPos)
        {
            UpdateBatches();
        }
        else
        {
            prevCamPos = cam.transform.position;
            UpdateBatchList();
            UpdateBatches();
        }
        RenderBatches();
        prevCamPos = cam.transform.position;
    }

    private void UpdateBatches()
    {
        foreach (var batch in batchesToUpdate)
        {
            foreach (var cloud in batch)
            {
                //Get noise size based on clouds pos, noise texture scale, and our offset amount
                float size = Mathf.PerlinNoise(cloud.x * texScale + offsetX, 
                                               cloud.y * texScale + offsetY);

                //If our cloud has a size that's above our visible cloud threashold we need to show it
                if (size > minNoiseSize)
                {
                    //Get the current scale of the cloud
                    float localScaleX = cloud.scale.x;

                    //Activate any clouds
                    if (!cloud.isActive)
                    {
                        cloud.SetActive(true);
                        cloud.scale = Vector3.zero;
                    }
                    //If not max size, scale up
                    if (localScaleX < maxScale)
                    {
                        ScaleCloud(cloud, 1);

                        //Limit our max size
                        if (cloud.scale.x > maxScale)
                        {
                            cloud.scale = new Vector3(maxScale, maxScale, maxScale);
                        }
                    }
                }
                //Active and it shouldn't be, let's scale down
                else if (size < minNoiseSize)
                {
                    float localScaleX = cloud.scale.x;
                    ScaleCloud(cloud, -1);

                    //When the cloud is reallllly small we can just set it to 0 and hide it
                    if (localScaleX <= 0.1)
                    {
                        cloud.SetActive(false);
                        cloud.scale = Vector3.zero;
                    }
                }
            }
        }
    }

    //This method sets cloud to a new size
    private void ScaleCloud(CloudData cloud, int direciton)
    {
        cloud.scale += new Vector3(sizeScale * Time.deltaTime * direciton, sizeScale * Time.deltaTime * direciton, sizeScale * Time.deltaTime * direciton);
    }

    //This method clears our batchesToUpdate list because we only want visible batches within this list
    private void UpdateBatchList()
    {
        //Clears our list
        batchesToUpdate.Clear();

        //Loop through all the generated batches
        foreach (var batch in batches)
        {
            //If a single cloud is within range we need to add the batch to the update list
            if (CheckForActiveBatch(batch))
            {
                batchesToUpdate.Add(batch);
            }
        }
    }

    //This method loops through all the batches to update and draws their meshes to the screen
    private void RenderBatches()
    {
        foreach (var batch in batchesToUpdate)
        {
            Graphics.DrawMeshInstanced(cloudMesh, 0, cloudMat, batch.Select((a) => a.matrix).ToList());
        }
    }
}
