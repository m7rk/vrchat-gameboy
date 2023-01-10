
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
public enum KeyCode
{
    RIGHT, DOWN, LEFT, UP, A, B, SELECT, START
}

public class GBButton : UdonSharpBehaviour
{
 
    public KeyCode keyCode;
    public JOYPAD jp;
    public float timer;
    // no invoke, so reset timer...
    public override void Interact()
    {
        jp.handleKeyDown(keyCode);
        timer = 2f;
    }

    public void Update()
    {
        timer -= Time.deltaTime;
        if(timer <= 0)
        {
            jp.handleKeyUp(keyCode);
        }
    }
}
