﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VoxelEngine.GUI {

    public class GuiScreenWaiting : GuiScreen {
        private List<string> tipList;
        public Text textTip;

        public void Awake() {
            this.tipList = new List<string>();
            this.tipList.Add("Tip 1");
            this.tipList.Add("Tip 2");
            this.tipList.Add("Tip 3");
        }

        public void OnEnable() {
            //TODO randomize the panel position
            this.textTip.text = this.getRandomTip();
        }

        private string getRandomTip() {
            return this.tipList[Random.Range(0, this.tipList.Count)];
        }
    }
}