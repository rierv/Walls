using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class StartButton : MonoBehaviour
{
    public Text gridLenght, gridHeight, speed, blocks, badBoost, goodBoost, boost, freeze, acceleration, xStart, yStart, xEnd, yEnd;
    public Toggle blockRegeneration;
    // Start is called before the first frame update
    public void startGame() {
        if (Scenes.parameters == null)
        {
            Scenes.setParam("gridLenght", gridLenght.text);
            Scenes.setParam("gridHeight", gridHeight.text);
            Scenes.setParam("xStart", xStart.text);
            Scenes.setParam("yStart", yStart.text);
            Scenes.setParam("xEnd", xEnd.text);
            Scenes.setParam("yEnd", yEnd.text);
            Scenes.setParam("speed", speed.text);
            Scenes.setParam("blocks", blocks.text);
            Scenes.setParam("boost", boost.text);
            Scenes.setParam("freeze", freeze.text);
            Scenes.setParam("acceleration", acceleration.text);
            Scenes.setParam("blockRegeneration", blockRegeneration.isOn+"");

        }
        //Scenes.setParam("badBoost", badBoost.text);
        //Scenes.setParam("goodBoost", goodBoost.text);
        Scenes.Load("GameScene", Scenes.parameters);
    }
    public void resetParameters()
    {
        Scenes.parameters = null;
    }
}
