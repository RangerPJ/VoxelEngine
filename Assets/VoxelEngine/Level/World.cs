﻿using UnityEngine;
using System.Collections.Generic;
using VoxelEngine.Util;
using VoxelEngine.Generation;
using VoxelEngine.Entities;
using VoxelEngine.Containers;
using VoxelEngine.Blocks;
using VoxelEngine.Items;
using fNbt;
using VoxelEngine.TileEntity;
using VoxelEngine.Generation.Caves;
using System;
using VoxelEngine.ChunkLoaders;
using VoxelEngine.Entities.Registry;
using VoxelEngine.Level.Light;

namespace VoxelEngine.Level {

    public class World : MonoBehaviour {

        public Dictionary<ChunkPos, Chunk> loadedChunks;
        public WorldGeneratorBase generator;
        public WorldData worldData;
        public NbtIOHelper nbtIOHelper;
        public List<Entity> entityList;

        public Transform chunkWrapper;
        private Transform entityWrapper;
        public Transform tileEntityWrapper;

        private WorldLighter lighter;

        // Acts like a constructor.
        public void initWorld(WorldData data) {
            this.worldData = data;
            this.loadedChunks = new Dictionary<ChunkPos, Chunk>();
            this.entityList = new List<Entity>();
            this.nbtIOHelper = new NbtIOHelper(this.worldData);
            this.generator = WorldType.getFromId(this.worldData.worldType).getGenerator(this, this.worldData.seed);

            this.lighter = new WorldLighter(this);

            if (!this.nbtIOHelper.readGenerationData(this.generator)) {
                // Generate the generation data, and save it if there was any.
                if(this.generator.generateLevelData()) {
                    this.nbtIOHelper.writeGenerationData(this.generator);
                }
                this.worldData.spawnPos = this.generator.getSpawnPoint(this);

                if (this.worldData.writeToDisk) {
                    // Save the world data right away so we don't have a folder with chunks that is
                    // recognized as a save.
                    this.nbtIOHelper.writeWorldDataToDisk(this.worldData);
                }
            }

            // Create game objects to hold others in for inspector organization.
            this.chunkWrapper = this.createWrapper("CHUNKS");
            this.entityWrapper = this.createWrapper("ENTITIES");
            this.tileEntityWrapper = this.createWrapper("TILE_ENTITIES");
        }

        private void Update() {
            if(Main.isDeveloperMode && this.generator is WorldGeneratorCaves) {
                ((WorldGeneratorCaves)this.generator).debugDisplay();
            }
        }

        /// <summary>
        /// Spawns an entity into the world, loading its state from nbt and returns it.
        /// </summary>
        public Entity spawnEntity(RegisteredEntity registeredEntity, NbtCompound tag) {
            Entity entity = this.instantiateEntityPrefab(registeredEntity.getPrefab(), true);
            entity.readFromNbt(tag);
            return entity;
        }

        public Entity spawnEntity(RegisteredEntity registeredEntity, Vector3 position, Quaternion rotation) {
            Entity entity = this.instantiateEntityPrefab(registeredEntity.getPrefab(), true);
            entity.transform.position = position;
            entity.transform.rotation = rotation;
            return entity;
        }

        /// <summary>
        /// Spawns the player into the world and sets them up, including the audio, and returns the player.
        /// </summary>
        public EntityPlayer spawnPlayer() {
            EntityPlayer player = (EntityPlayer)this.instantiateEntityPrefab(EntityRegistry.player.getPrefab(), false);
            player.name = "Player";
            if(!this.nbtIOHelper.readPlayerFromDisk(player)) {
                // No player file was found, this must be a new world.
                player.transform.position = this.worldData.spawnPos;
                player.transform.rotation = Quaternion.Euler(0, UnityEngine.Random.Range(0, 360), 0);
                player.setupFirstTimePlayer();
            }

            SoundManager.setPlayerListenerRef(player.mainCamera.GetComponent<AudioListener>());

            return player;
        }

        /// <summary>
        /// Kills an entity, removing it from the world.
        /// </summary>
        public void killEntity(Entity entity) {
            this.entityList.Remove(entity);
            GameObject.Destroy(entity.gameObject);
        }

        /// <summary>
        /// Spawns a dropped item into the world.
        /// </summary>
        public EntityItem spawnItem(ItemStack stack, Vector3 position, Quaternion rotation, Vector3 force) {
            EntityItem entityItem = (EntityItem)this.spawnEntity(EntityRegistry.item, position, rotation);
            entityItem.setStack(stack);
            entityItem.rBody.AddForce(force, ForceMode.Impulse);

            return entityItem;
        }

        /// <summary>
        /// Loads a new chunk, loading it from disk if it exists, otherwise we generate a new one.
        /// </summary>
        public Chunk loadChunk(Chunk chunk, NewChunkInstructions instructions) {
            chunk.initChunk(this, instructions);

            this.loadedChunks.Add(chunk.chunkPos, chunk);

            if (!this.nbtIOHelper.readChunkFromDisk(chunk)) {
                // Generate chunk and compute lighting.
                this.generator.generateChunk(chunk);

                Chunk adjacentChunk;
                for (int x = -1; x <= 1; x++) {
                    for (int y = -1; y <= 1; y++) {
                        for (int z = -1; z <= 1; z++) {
                            if (!(x == 0 && y == 0 && z == 0)) { // Not the middle chunk.
                                adjacentChunk = this.getChunk(new ChunkPos(chunk.chunkPos.x + x, chunk.chunkPos.y + y, chunk.chunkPos.z + z));
                                if (adjacentChunk != null && !adjacentChunk.hasDoneGen2) {
                                    this.tryPhase2OnChunk(adjacentChunk);
                                }
                            }
                        }
                    }
                }

                // It's possible that this chunk itself needs phase 2.
                this.tryPhase2OnChunk(chunk);
            }
            return chunk;
        }

        public void unloadChunk(Chunk chunk) {
            this.saveChunk(chunk, true);

            // Destroy the TileEntity gameObjects within the chunk.
            foreach(TileEntityBase te in chunk.tileEntityDict.Values) {
                if(te is TileEntityGameObject) {
                    GameObject.Destroy(((TileEntityGameObject)te).gameObject);
                }
            }

            this.loadedChunks.Remove(chunk.chunkPos);
        }

        public Chunk getChunk(ChunkPos pos) {
            Chunk chunk = null;
            this.loadedChunks.TryGetValue(pos, out chunk);
            return chunk;
        }

        public Chunk getChunk(BlockPos pos) {
            return this.getChunk(pos.x, pos.y, pos.z);
        }

        public Chunk getChunk(int worldX, int worldY, int worldZ) {
            return this.getChunk(new ChunkPos(
                MathHelper.floor(worldX / Chunk.SIZEf),
                MathHelper.floor(worldY / Chunk.SIZEf),
                MathHelper.floor(worldZ / Chunk.SIZEf)));
        }

        public Block getBlock(BlockPos pos) {
            return this.getBlock(pos.x, pos.y, pos.z);
        }

        public Block getBlock(int x, int y, int z) {
            Chunk chunk = this.getChunk(x, y, z);
            if (chunk != null) {
                return chunk.getBlock(x - chunk.worldPos.x, y - chunk.worldPos.y, z - chunk.worldPos.z);
            } else {
                return Block.air;
            }
        }

        /// <summary>
        /// Sets a block.  Using a meta of -1 will not change the meta.
        /// </summary>
        public void setBlock(BlockPos pos, Block newblock, int newMeta = -1, bool updateNeighbors = true, bool updateLighting = true) {
            this.setBlock(pos.x, pos.y, pos.z, newblock, newMeta, updateNeighbors, updateLighting);
        }

        /// <summary>
        /// Sets a block.  Passing null for new block will not change the block.  Using a meta of -1 will not change the meta.
        /// </summary>
        public void setBlock(int x, int y, int z, Block newBlock, int newMeta = -1, bool updateNeighbors = true, bool updateLighting = true) {
            Chunk chunk = this.getChunk(x, y, z);
            if (chunk != null) {
                // Position of the setBlock event within the chunk.
                int localChunkX = x - chunk.worldPos.x;
                int localChunkY = y - chunk.worldPos.y;
                int localChunkZ = z - chunk.worldPos.z;

                BlockPos newBlockWorldPos = new BlockPos(x, y, z);

                if(newBlock != null) {
                    Block oldBlock = chunk.getBlock(localChunkX, localChunkY, localChunkZ);
                    int oldBlockMeta = chunk.getMeta(localChunkX, localChunkY, localChunkZ);
                    oldBlock.onDestroy(this, newBlockWorldPos, oldBlockMeta);

                    chunk.setBlock(localChunkX, localChunkY, localChunkZ, newBlock);

                    if(newMeta == -1) {
                        newMeta = 0; // If the block is being changed, assume it should be 0... ?
                    }
                }

                // Set meta if it's specified.  -1 means don't change.
                if (newMeta != -1) {
                    chunk.setMeta(localChunkX, localChunkY, localChunkZ, newMeta);
                }

                if(newBlock != null) {
                    newBlock.onPlace(this, newBlockWorldPos, (newMeta == -1 ? chunk.getMeta(localChunkX, localChunkY, localChunkZ) : newMeta));
                }

                // Update surrounding blocks.
                if (updateNeighbors) {
                    Direction dir;
                    for(int i = 0; i < 6; i++) {
                        dir = Direction.all[i];
                        BlockPos shiftedPos = newBlockWorldPos.move(dir);
                        this.getBlock(shiftedPos).onNeighborChange(this, shiftedPos, this.getMeta(shiftedPos), dir.getOpposite());
                    }
                    this.dirtyChunkIfEqual(x - chunk.worldPos.x, 0,              x - 1, y, z);
                    this.dirtyChunkIfEqual(x - chunk.worldPos.x, Chunk.SIZE - 1, x + 1, y, z);
                    this.dirtyChunkIfEqual(y - chunk.worldPos.y, 0,              x, y - 1, z);
                    this.dirtyChunkIfEqual(y - chunk.worldPos.y, Chunk.SIZE - 1, x, y + 1, z);
                    this.dirtyChunkIfEqual(z - chunk.worldPos.z, 0,              x, y, z - 1);
                    this.dirtyChunkIfEqual(z - chunk.worldPos.z, Chunk.SIZE - 1, x, y, z + 1);
                }

                // Update lighting.
                if (newBlock != null && updateLighting) {
                    this.lighter.updateLighting(newBlock.emittedLight, x, y, z);
                }
            }
        }

        public int getMeta(BlockPos pos) {
            return this.getMeta(pos.x, pos.y, pos.z);
        }

        public int getMeta(int x, int y, int z) {
            Chunk chunk = this.getChunk(x, y, z);
            return chunk != null ? chunk.getMeta(x - chunk.worldPos.x, y - chunk.worldPos.y, z - chunk.worldPos.z) : 0;
        }

        /// <summary>
        /// Returns the light at (x, y, z) or 0 if the chunk is not loaded
        /// </summary>
        public int getLight(int x, int y, int z) {
            Chunk chunk = this.getChunk(x, y, z);
            if(chunk != null) {
                return chunk.getLight(x - chunk.worldPos.x, y - chunk.worldPos.y, z - chunk.worldPos.z);
            } else {
                return 0;
            }
        }

        public TileEntityBase getTileEntity(int x, int y, int z) {
            return this.getTileEntity(new BlockPos(x, y, z));
        }
        
        /// <summary>
        /// Returns the TileEntity at pos, or null if it can't be found.
        /// </summary>
        public TileEntityBase getTileEntity(BlockPos pos) {
            Chunk chunk = this.getChunk(pos);
            if(chunk != null) {
                return this.getChunk(pos).tileEntityDict[pos];
            }
            return null;
        }

        /// <summary>
        /// Adds the passed TileEntity into the world, throwing an exception if there is already a TileEntity there.
        /// </summary>
        public void addTileEntity(BlockPos pos, TileEntityBase tileEntity) {
            Chunk chunk = this.getChunk(pos);
            if(chunk != null) {
                if(chunk.tileEntityDict.ContainsKey(pos)) {
                    Debug.Log(chunk.tileEntityDict[pos]);
                    throw new Exception("Error!  Something tried to add a second TileEntity at " + pos.ToString() + "!");
                }
                this.getChunk(pos).tileEntityDict.Add(pos, tileEntity);
            }
        }

        /// <summary>
        /// Removes a TileEntity from the world.  Warning!  This does not provide any sort of cleanup!
        /// </summary>
        public void removeTileEntity(BlockPos pos) {
            Chunk chunk = this.getChunk(pos);
            if(chunk != null) {
                chunk.tileEntityDict.Remove(pos);
            }
        }

        /// <summary>
        /// Like set block, but makes a dropped item appear.  Note, this calls World.setBlock to actually set the block to air.
        /// DropChance is 0 1, with high values making drops more likely.
        /// </summary>
        public void breakBlock(BlockPos pos, ItemTool brokenWith, float dropChance = 1) {
            Block block = this.getBlock(pos);
            ItemStack[] dropList = block.getDrops(this, pos, this.getMeta(pos), brokenWith);
            float f = 0.5f;
            if (dropList != null) {
                for(int i = 0; i < dropList.Length; i++) {
                    if(dropChance != 1 || !(block is BlockTileEntity)) {
                        if(UnityEngine.Random.Range(0f, 1f) > dropChance) {
                            continue;
                        }
                    }
                    Vector3 offset = new Vector3(UnityEngine.Random.Range(-f, f), UnityEngine.Random.Range(-f, f), UnityEngine.Random.Range(-f, f));
                    this.spawnItem(dropList[i], pos.toVector() + offset, EntityItem.randomRotation(), new Vector3(0, 0, 0));
                }
            }
            this.setBlock(pos, Block.air);
        }

        /// <summary>
        /// Saves the entire world, including chunks, players and the world data.  Deletes entities if the world is closing.
        /// </summary>
        public void saveEntireWorld(bool deleteEntities) {
            //TODO http://answers.unity3d.com/questions/850451/capturescreenshot-without-ui.html To hide UI
            //this.nbtIOHelper.writeWorldImageToDisk();

            this.nbtIOHelper.writeWorldDataToDisk(this.worldData);
            this.nbtIOHelper.writePlayerToDisk(Main.singleton.player);

            foreach (Chunk chunk in this.loadedChunks.Values) {
                this.saveChunk(chunk, deleteEntities);
            }
        }

        public void scheduleFutureTick(BlockPos pos, float secondsUntil) {
            Chunk c = this.getChunk(pos);
            if(c != null) {
                c.scheduledTicks.Add(new ScheduledTick(pos, secondsUntil));
            } else {
                Debug.LogWarning("A Block tried to schedule a tick at " + pos.ToString() + "!  This is out of the world!  Ignoring!");
            }
        }

        public void makeExplosion(IExplosiveObject obj, Vector3 point) {
            BlockPos explosionOrgin = new BlockPos(point);
            BlockPos pos;
            int x, y, z;
            float size = obj.getExplosionSize();
            int radius = Mathf.CeilToInt(size);
            Block block;

            // Break Blocks.
            for(x = -radius; x <= radius; x++) {
                for (y = -radius; y <= radius; y++) {
                    for (z = -radius; z <= radius; z++) {
                        if(Vector3.Distance(new Vector3(x, y, z), Vector3.zero) <= size) {
                            pos = explosionOrgin + new BlockPos(x, y, z);
                            block = this.getBlock(pos);
                            if (block is IExplosiveObject) {
                                this.setBlock(pos, Block.air);
                                this.makeExplosion((IExplosiveObject)block, pos.toVector());
                            } else {
                                this.breakBlock(pos, null, 0.75f);
                            }
                        }
                    }
                }
            }

            // Damage Entites.
            Entity entity;
            Collider[] colliders = Physics.OverlapSphere(point, size, (Layers.ENTITY | Layers.ENTITY_ITEM | Layers.ENTITY_PLAYER));
            for(int i = 0; i < colliders.Length; i++) {
                entity = colliders[i].GetComponent<Entity>();
                if(entity != null) {
                    if(entity is EntityLiving) {
                        ((EntityLiving)entity).damage((int)(size * 4), "TNT.ERROR");
                    } else if(entity is EntityItem && ((EntityItem)entity).timeAlive == EntityItem.START_TIME) {
                        continue;
                    } else {
                        this.killEntity(entity);
                    }
                }
            }
        }

        /// <summary>
        /// Dirties every loaded Chunk so they are rebaked at the end of the frame.
        /// </summary>
        public void rebakeWorld() {
            foreach (Chunk chunk in this.loadedChunks.Values) {
                chunk.setDirty();
            }
        }

        // Unused and untested.
        private void makeExplosionRay(Vector3 orgin, Vector3 direction, float distance) {
            float traveled = 0f;

            BlockPos lastPos = new BlockPos(orgin);
            BlockPos pos;
            direction /= 2;

            while (traveled <= distance) {
                orgin += direction;
                pos = new BlockPos(orgin);
                if (!(pos.Equals(lastPos))) {
                    // Got to a new block.
                    if (this.getBlock(pos) == Block.air) {
                        if (UnityEngine.Random.Range(0, 3) == 0) {
                            this.breakBlock(pos, null);
                        }
                        else {
                            this.setBlock(pos, Block.air);
                        }
                    }
                    lastPos = pos;
                }
                traveled += 0.5f;
            }
        }

        /// <summary>
        /// Returns true if all the adjacent chunks are loaded.
        /// </summary>
        private bool allAdjacentLoaded(ChunkPos chunkPos) {
            for (int x = -1; x <= 1; x++) {
                for (int y = -1; y <= 1; y++) {
                    for (int z = -1; z <= 1; z++) {
                        if(!(x == 0 && y == 0 && z == 0)) {
                            if (this.getChunk(new ChunkPos(chunkPos.x + x, chunkPos.y + y, chunkPos.z + z)) == null) {
                                return false;
                            }
                        }
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Completly lights the passed chunk.  Called on newly generated chunks.
        /// </summary>
        private void lightChunk(Chunk chunk) {
            int emittedLight;
            for (int x = 0; x < Chunk.SIZE; x++) {
                for (int y = 0; y < Chunk.SIZE; y++) {
                    for (int z = 0; z < Chunk.SIZE; z++) {
                        emittedLight = chunk.getBlock(x, y, z).emittedLight;
                        if (emittedLight > 0) {
                            this.lighter.updateLighting(emittedLight, chunk.worldPos.x + x, chunk.worldPos.y + y, chunk.worldPos.z + z);
                        }
                    }
                }
            }
        }
        
        private void dirtyChunkIfEqual(int value1, int value2, int x, int y, int z) {
            if (value1 == value2) {
                Chunk chunk = getChunk(x, y, z);
                if (chunk != null) {
                    chunk.setDirty();
                }
            }
        }

        /// <summary>
        /// Saves the passed chunk to disk.
        /// </summary>
        private void saveChunk(Chunk chunk, bool deleteEntities) {
            NbtCompound tag = new NbtCompound("chunk");
            chunk.writeToNbt(tag, deleteEntities);
            this.nbtIOHelper.writeChunkToDisk(chunk, tag);
        }

        /// <summary>
        /// If all the adjacent chunks are loaded, do phase 2.
        /// </summary>
        private void tryPhase2OnChunk(Chunk chunk) {
            if(this.allAdjacentLoaded(chunk.chunkPos)) {
                this.generator.populateChunk(chunk);
                this.lightChunk(chunk);
                chunk.hasDoneGen2 = true;
            }
        }

        private Transform createWrapper(string name) {
            Transform trans = new GameObject(name).transform;
            trans.parent = this.transform;
            return trans;
        }

        private Entity instantiateEntityPrefab(GameObject prefab, bool placeInWrapper) {
            GameObject gameObject = GameObject.Instantiate(prefab);
            if (placeInWrapper) {
                gameObject.transform.parent = this.entityWrapper;
            }
            Entity entity = gameObject.GetComponent<Entity>();
            entity.world = this;
            this.entityList.Add(entity);
            entity.onConstruct();
            return entity;
        }

        public void setLight(int x, int y, int z, int level) {
            Chunk chunk = this.getChunk(x, y, z);
            if (chunk != null) {
                chunk.setLight(x - chunk.worldPos.x, y - chunk.worldPos.y, z - chunk.worldPos.z, level);
            }
        }
    }
}