﻿using fNbt;

namespace VoxelEngine.Entities {

    public class EntityLiving : Entity {

        public int health;
        private int maxHealth;

        protected new void Start() {
            this.setHealth(this.maxHealth);

            base.Start();
        }

        /// <summary>
        /// Sets an entities held, clamping it between 0 and their max.
        /// </summary>
        public virtual void setHealth(int amount) {
            if (amount > this.maxHealth) {
                amount = this.maxHealth;
            }
            else if (amount < 0) {
                amount = 0;
            }
            this.health = amount;
        }

        /// <summary>
        /// Returns true if the entity was killed by this damage.
        /// </summary>
        public virtual bool damage(int amount, string message) {
            this.setHealth(this.health - amount);
            if (this.health <= 0) {
                this.world.killEntity(this);
                return true;
            }
            return false;
        }

        public override NbtCompound writeToNbt(NbtCompound tag) {
            base.writeToNbt(tag);

            tag.Add(new NbtInt("health", this.health));

            return tag;
        }

        public override void readFromNbt(NbtCompound tag) {
            base.readFromNbt(tag);

            this.health = tag.Get<NbtInt>("health").IntValue;
        }

        /// <summary>
        /// Sets the entities max health.  Call from onConstruct.
        /// </summary>
        public void setMaxHealth(int max) {
            this.maxHealth = max;
        }
    }
}
