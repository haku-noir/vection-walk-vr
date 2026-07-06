using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore.Text;

/// <summary>
/// Ç«ÇÃé¿å±ÇçsÇ§Ç©Çä«óùÇ∑ÇÈÇΩÇﬂÇÃÉNÉâÉX
/// </summary>
public class SelectSphereExperiment : MonoBehaviour
{

    public enum Experiment{
        SeekAndLook,
        SeekAndMoveAndTouch
    };
    public Experiment experiment = Experiment.SeekAndLook;

    [SerializeField] SphereGenerator _spheregenerator;
    [SerializeField] ControllerTouchSphere _controllertouchsphere;

    public GameObject LHandAppearance;
    public GameObject RHandAppearance;

    private void Start()
    {
        if (LHandAppearance != null && RHandAppearance != null)
        {
            switch (experiment)
            {
                case Experiment.SeekAndLook:
                    LHandAppearance.SetActive(false);
                    RHandAppearance.SetActive(false);
                    _spheregenerator.enabled = true;
                    break;
                case Experiment.SeekAndMoveAndTouch:
                    LHandAppearance.SetActive(true);
                    RHandAppearance.SetActive(true);
                    _controllertouchsphere.enabled = true;
                    break;

            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        //switch (experiment)
        //{
        //    case Experiment.SeekAndLook:
        //        _spheregenerator.enabled = true;
        //        break;
        //    case Experiment.SeekAndMoveAndTouch:
        //        _controllertouchsphere.enabled = true;
        //        break;

        //}
    }
}
