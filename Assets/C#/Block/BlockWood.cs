﻿public class BlockWood : Block {

    public override TexturePos getTexturePos(Direction direction, byte meta) {
        TexturePos tile = new TexturePos(1, 1);
        if(direction == Direction.UP || direction == Direction.DOWN) {
            tile.x = 2;
        }
        return tile;
    }
}
