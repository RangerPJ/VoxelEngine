﻿using System.Collections.Generic;
using UnityEngine;
using VoxelEngine.Blocks;
using VoxelEngine.Util;

namespace VoxelEngine.Render.BlockRender {

    public class BlockRendererMesh : BlockRenderer {

        private GameObject prefab;
        private Vector3[] cachedMeshVerts;
        private int[] cachedMeshTris;
        private Vector2[] cachedMeshUVs;
        private Bounds[] colliderArray;

        private Vector3 offsetVector;
        private Quaternion modelRotation;
        private bool useMeshForCollision = true;

        public BlockRendererMesh(GameObject prefab) {
            this.prefab = prefab;
            List<Mesh> meshes = new List<Mesh>();
            List<Vector3> offsets = new List<Vector3>();

            this.extractMesh(this.prefab.transform, meshes, offsets);
            foreach(Transform trans in this.prefab.transform) {
                this.extractMesh(trans, meshes, offsets);
            }

            if(meshes.Count == 0) {
                Debug.Log("ERROR!  No MeshFilter components could be found on the Prefab!");
            } else if (meshes.Count == 1) {
                Mesh m = meshes[0];
                this.correctVerticeRotations(m.vertices);
                this.cachedMeshTris = m.triangles;
                this.cachedMeshUVs = m.uv;
            } else {
                List<Vector3> vertList = new List<Vector3>();
                List<int> triList = new List<int>();
                List<Vector2> uvList = new List<Vector2>();
                Vector3[] cachedVerts;
                for(int i = 0; i < meshes.Count; i++) {
                    Mesh m = meshes[i];
                    cachedVerts = m.vertices;
                    for(int j = 0; j < cachedVerts.Length; j++) {
                        vertList.Add((cachedVerts[j] + offsets[i]));
                    }
                    triList.AddRange(m.triangles);
                    uvList.AddRange(m.uv);
                }
                this.correctVerticeRotations(vertList.ToArray());
                this.cachedMeshTris = triList.ToArray();
                this.cachedMeshUVs = uvList.ToArray();
            }
        }

        public override void renderBlock(Block b, int meta, MeshBuilder meshBuilder, int x, int y, int z, int renderFace, Block[] surroundingBlocks) {
            int i;
            Vector3 vertex;
            Color color = RenderManager.instance.lightColors.getColorFromBrightness(meshBuilder.getLightLevel(0, 0, 0));

            // Add the colliders
            if(meshBuilder.autoGenerateColliders && !this.useMeshForCollision) { // Check useRenderDataForCol because it is false if we are rendering an item
                for(i = 0; i < this.colliderArray.Length; i++) {
                    meshBuilder.autoGenerateColliders = false;
                    meshBuilder.addColliderBox(this.colliderArray[i], x + this.offsetVector.x, y + this.offsetVector.y, z + this.offsetVector.z);
                }
            }

            // Add vertices
            int vertStart = meshBuilder.getVerticeCount();
            for(i = 0; i < this.cachedMeshVerts.Length; i++) {
                vertex = this.cachedMeshVerts[i];
                meshBuilder.addRawVertex(new Vector3(vertex.x + x + this.offsetVector.x, vertex.y + y + this.offsetVector.y, vertex.z + z + this.offsetVector.z));
                meshBuilder.addRawVertexColor(color);
            }

            // Add triangles
            for (i = 0; i < this.cachedMeshTris.Length; i++) {
                meshBuilder.addRawTriangle(vertStart + this.cachedMeshTris[i]);
            }

            // Add UVs
            for(i = 0; i < this.cachedMeshUVs.Length; i++) {
                meshBuilder.addRawUv(this.cachedMeshUVs[i]);
            }            

            meshBuilder.autoGenerateColliders = true;
        }

        private void extractMesh(Transform t, List<Mesh> meshes, List<Vector3> offsets) {
            MeshFilter filter = t.GetComponent<MeshFilter>();
            if (filter != null) {
                meshes.Add(filter.sharedMesh);
                offsets.Add(t.localPosition);
            }
        }

        /// <summary>
        /// Corrects the vertices rotation by rotating them -90 degrees because of Blender.
        /// </summary>
        private void correctVerticeRotations(Vector3[] verts) {
            this.cachedMeshVerts = new Vector3[verts.Length];
            Quaternion q = Quaternion.Euler(-90, 0, 0);
            Vector3 v;
            for (int i = 0; i < verts.Length; i++) {
                v = verts[i];
                this.cachedMeshVerts[i] = MathHelper.rotateVecAround(v, Vector3.zero, q);
            }
        }

        /// <summary>
        /// Rotates the model by the passed Quaternion.
        /// </summary>
        public BlockRendererMesh setRotation(Quaternion rot) {
            this.modelRotation = rot;
            return this;
        }

        /// <summary>
        /// Offsets the model by the passed Vector3.
        /// </summary>
        public BlockRendererMesh setOffsetVector(Vector3 vec) {
            this.offsetVector = vec;
            return this;
        }

        /// <summary>
        /// Marks the block to use collider component attached to the prefab as the collider.
        /// </summary>
        public BlockRendererMesh useColliderComponent() {
            this.useMeshForCollision = false;
            BoxCollider[] bc = this.prefab.GetComponents<BoxCollider>();
            this.colliderArray = new Bounds[bc.Length];
            for (int i = 0; i < bc.Length; i++) {
                BoxCollider b = bc[i];
                this.colliderArray[i] = new Bounds(new Vector3(b.center.x, b.center.y, b.center.z), new Vector3(b.size.x, b.size.y, b.size.z));
            }
            return this;
        }
    }
}
