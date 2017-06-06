﻿using UnityEngine;
using VoxelEngine.Items;
using VoxelEngine.Render.BlockRender;

namespace VoxelEngine.Render {

    public class RenderManager {
        
        public static RenderManager instance;

        public static BlockRenderer CUBE = new BlockRendererPrimitiveCube();
        public static BlockRenderer CROSS = new BlockRendererCorn();
        public static BlockRenderer RAIL = new BlockRendererRail();
        public static BlockRenderer FENCE = new BlockRendererFence();
        public static BlockRenderer LADDER = new BlockRendererLadder();
        public static BlockRenderer LANTERN = new BlockRendererMesh(References.list.lanternPrefab).setRenderInWorld(false);
        public static BlockRenderer TORCH = new BlockRendererTorch();
        public static BlockRenderer MUSHROOM = new BlockRendererMesh(References.list.mushroomPrefab).useRandomMirror().setOffsetVector(new Vector3(0, -0.5f, 0)).useColliderComponent();
        public static BlockRenderer CHEST = new BlockRendererChest(); // new BlockRendererMesh(References.list.chestPrefab).setRenderInWorld(false);
        public static BlockRenderer SLAB = new BlockRendererSlab();
        public static BlockRenderer FLUID = new BlockRendererFluid();
        public static BlockRenderer BED = new BlockRendererBed();

        /// <summary> This is set in the Awake method of HudCamera.cs </summary>
        public HudCamera hudCamera;
        public LightHelper lightHelper;
        private MeshBuilder reusableMeshData;

        public RenderManager() {
            RenderManager.instance = this;

            this.lightHelper = new LightHelper(References.list.lightColorSheet);
            this.reusableMeshData = new MeshBuilder();

            Item.initBlockItems();
            this.preRenderItems();
        }

        /// <summary>
        /// Returns a ready to use meshBuilder.
        /// </summary>
        public MeshBuilder getMeshBuilder() {
            this.reusableMeshData.cleanup();
            return this.reusableMeshData;
        }

        /// <summary>
        /// Prerenders all the items, saving the meshes in Item.preRenderedMeshes
        /// </summary>
        private void preRenderItems() {
            for (int i = 0; i < Item.ITEM_LIST.Length; i++) {
                Item item = Item.ITEM_LIST[i];
                if (item != null) {
                    item.preRenderedMeshes = new Mesh[item.getStatesUsed()];
                    for (int j = 0; j < item.preRenderedMeshes.Length; j++) {
                        item.preRenderedMeshes[j] = item.itemRenderer.renderItem(this, item, j);
                    }
                }
            }
        }

        /// <summary>
        /// Returns the correct material that corresponds with the block/item id
        /// </summary>
        public static Material getMaterial(int id) {
            return id < 256 ? References.list.blockMaterial : References.list.itemMaterial;
        }
    }
}