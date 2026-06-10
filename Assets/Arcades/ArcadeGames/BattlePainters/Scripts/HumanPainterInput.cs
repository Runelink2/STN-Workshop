using UnityEngine;

public sealed class HumanPainterInput : IPainterInputSource {
    private readonly KeyCode leftKey;
    private readonly KeyCode rightKey;

    public HumanPainterInput(KeyCode leftKey, KeyCode rightKey) {
        this.leftKey = leftKey;
        this.rightKey = rightKey;
    }

    public float GetTurnInput(float deltaTime, PainterInputContext context) {
        bool leftPressed = Input.GetKey(leftKey);
        bool rightPressed = Input.GetKey(rightKey);

        if (leftPressed == rightPressed) {
            return 0.0f;
        }

        return leftPressed ? 1.0f : -1.0f;
    }
}
