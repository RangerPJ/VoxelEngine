﻿public class BlockWood : Block {
    //public override BlockModel getModel(byte meta) {
    //    return Block.MODEL_CROSS;
    //}

    public override TexturePos getTexturePos(Direction direction, byte meta) {
        TexturePos tile = new TexturePos();
        if(direction == Direction.UP || direction == Direction.DOWN) {
            tile.x = 2;
            tile.y = 1;
        } else {
            tile.x = 1;
            tile.y = 1;
        }
        return tile;
    }
}
