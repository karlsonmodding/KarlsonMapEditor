using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

namespace KarlsonMapEditor
{
    public class Enemy_ProjectPos : MonoBehaviour
    {
        private NavMeshAgent agent;
        public float delta = 0;

        public void Start()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.updatePosition = false;
        }

        public void Update()
        {
            if (agent == null)
            {
                Destroy(this);
                return;
            }
            transform.position = agent.nextPosition - new Vector3(0, delta, 0);
        }
    }
}
