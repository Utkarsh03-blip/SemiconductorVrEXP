using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace PNJunctionLab
{
    /// <summary>
    /// Periodic table valence groups used across the doping benches.
    /// </summary>
    public enum DopantValenceGroup
    {
        GroupIII_Acceptor = 3,
        GroupIV_Intrinsic = 4,
        GroupV_Donor = 5
    }

    /// <summary>
    /// Doping polarity produced when paired with Silicon.
    /// </summary>
    public enum ExtrinsicDopingKind
    {
        None = 0,
        NType = 1,
        PType = 2
    }

    /// <summary>
    /// Represents a grabbable chemical dopant vial on Stations 2 and 3 (e.g., P, As, Sb, B, Al, Ga, In, Ge, C)
    /// storing periodic valence group metadata for diffusion furnace validation.
    /// </summary>
    public class DopantVialItem : MonoBehaviour
    {
        [SerializeField] private string elementSymbol = "P";
        [SerializeField] private string elementName = "Phosphorus";
        [SerializeField] private int atomicNumber = 15;
        [SerializeField] private DopantValenceGroup valenceGroup = DopantValenceGroup.GroupV_Donor;
        [SerializeField] private XRGrabInteractable grabInteractable;
        [SerializeField] private RespawnableItem respawnableItem;

        public string ElementSymbol => elementSymbol;
        public string ElementName => elementName;
        public int AtomicNumber => atomicNumber;
        public DopantValenceGroup ValenceGroup => valenceGroup;
        public XRGrabInteractable GrabInteractable => grabInteractable;

        public ExtrinsicDopingKind ResultingDopingKind
        {
            get
            {
                switch (valenceGroup)
                {
                    case DopantValenceGroup.GroupV_Donor:
                        return ExtrinsicDopingKind.NType;
                    case DopantValenceGroup.GroupIII_Acceptor:
                        return ExtrinsicDopingKind.PType;
                    default:
                        return ExtrinsicDopingKind.None;
                }
            }
        }

        private void Awake()
        {
            if (grabInteractable == null)
            {
                grabInteractable = GetComponent<XRGrabInteractable>();
            }

            if (respawnableItem == null)
            {
                respawnableItem = GetComponent<RespawnableItem>();
            }
        }

        /// <summary>
        /// Assigns chemical identity for a dopant vial during builder setup or tests.
        /// </summary>
        public void Configure(string symbol, string fullName, int z, DopantValenceGroup group,
            XRGrabInteractable grab, RespawnableItem respawn)
        {
            elementSymbol = symbol;
            elementName = fullName;
            atomicNumber = z;
            valenceGroup = group;
            grabInteractable = grab;
            respawnableItem = respawn;
        }

        /// <summary>
        /// Returns this vial to its home shelf slot cleanly (exiting any active socket or hand selection).
        /// </summary>
        public void ReturnToShelf()
        {
            if (respawnableItem != null)
            {
                respawnableItem.Respawn();
            }
        }

        /// <summary>
        /// Builds a student-friendly explanation when this vial does not match a target doping polarity.
        /// </summary>
        public string BuildWrongGroupMessage(ExtrinsicDopingKind expectedKind)
        {
            if (valenceGroup == DopantValenceGroup.GroupIV_Intrinsic)
            {
                return $"{elementName} ({elementSymbol}) is in Group IV with 4 valence electrons — just like Silicon, all 4 bonds pair up cleanly, adding zero extra free carriers!";
            }

            if (expectedKind == ExtrinsicDopingKind.NType && valenceGroup == DopantValenceGroup.GroupIII_Acceptor)
            {
                return $"{elementName} ({elementSymbol}) is a Group III acceptor (3 valence e-) — it leaves missing bonds (holes) for P-type Si. For N-type, use a Group V donor (P, As, or Sb)!";
            }

            if (expectedKind == ExtrinsicDopingKind.PType && valenceGroup == DopantValenceGroup.GroupV_Donor)
            {
                return $"{elementName} ({elementSymbol}) is a Group V donor (5 valence e-) — it supplies free electrons for N-type Si. For P-type, use a Group III acceptor (B, Al, Ga, or In)!";
            }

            return $"{elementName} ({elementSymbol}) does not match the required valence group.";
        }
    }
}
