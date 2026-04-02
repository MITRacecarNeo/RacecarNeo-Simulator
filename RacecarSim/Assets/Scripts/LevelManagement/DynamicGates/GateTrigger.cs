using UnityEngine;

public class GateTrigger : MonoBehaviour
{
    public TriggerGate[] gates; // The gates to raise when the car drives over this trigger

    private void OnTriggerEnter(Collider other)
    {
        Racecar racecar = other.GetComponentInParent<Racecar>();
        if (racecar != null)
        {
            foreach (TriggerGate gate in gates)
            {
                if (gate != null)
                {
                    gate.Raise();
                }
            }
        }
    }
}
