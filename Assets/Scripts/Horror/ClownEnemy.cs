using System.Collections.Generic;
using UnityEngine;

namespace SgiVr.Horror
{
    /// <summary>
    /// IA del payaso: patrulla la casa, si te ve se enfurece y te persigue, y te ataca de cerca.
    /// Usa las puertas guardadas en HouseLayout para moverse entre habitaciones.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ClownEnemy : MonoBehaviour, IDamageable
    {
        [Header("Vida y daño")]
        public int health = 4;
        public int attackDamage = 25;
        public float attackCooldown = 1.8f;
        [Tooltip("Segundos desde que empieza el ataque hasta que golpea.")]
        public float attackHitDelay = 0.5f;
        [Tooltip("Segundos que se queda en el suelo antes de levantarse otra vez.")]
        public float reviveTime = 20f;
        [Tooltip("Segundos que tarda en ponerse de pie.")]
        public float getUpTime = 1.2f;

        [Header("Movimiento")]
        public float walkSpeed = 1.0f;
        public float runSpeed = 2.6f;
        public float turnSpeed = 300f;

        [Header("Percepción")]
        public float detectionRange = 10f;
        [Tooltip("A esta distancia te detecta aunque no te vea.")]
        public float hearingRange = 2.5f;
        public float attackRange = 1.4f;
        [Tooltip("Segundos sin verte antes de volver a patrullar.")]
        public float loseInterestTime = 6f;

        [Header("Estados del Animator (nombres exactos)")]
        public string idleState = "idle_1";
        public string walkState = "Walk";
        public string runState = "run_1";
        public string attackState = "attack_1";
        public string hitState = "hit_1";
        public string deathState = "death";
        public string rageState = "rage";

        enum State { Patrol, Rage, Chase, Attack, Hit, Dead, GettingUp }

        int maxHealth;

        // Datos para el HUD.
        public int Health => Mathf.Max(0, health);
        public int MaxHealth => Mathf.Max(1, maxHealth > 0 ? maxHealth : health);
        public bool IsDown => state == State.Dead;
        public float SecondsToRevive => IsDown ? Mathf.Max(0f, stateTimer) : 0f;
        public float ReviveProgress => IsDown ? 1f - Mathf.Clamp01(stateTimer / Mathf.Max(0.01f, reviveTime)) : 1f;

        void Awake()
        {
            maxHealth = Mathf.Max(1, health);
        }

        State state = State.Patrol;
        CharacterController controller;
        Animator animator;
        HouseLayout house;
        readonly List<Vector3> path = new List<Vector3>();
        int pathIndex;
        float repathTimer;
        float stateTimer;
        float idleTimer;
        float lastSeenTime = -999f;
        bool attackLanded;
        bool movedThisFrame;
        Vector3 patrolTarget;
        string currentAnim;
        Vector3 lastPosition;
        float stuckTimer;
        float nudgeTimer;
        Vector3 nudgeDir;

        void Start()
        {
            controller = GetComponent<CharacterController>();
            animator = GetComponentInChildren<Animator>();
            if (animator) animator.applyRootMotion = false;
            house = HouseLayout.Find();
            lastPosition = transform.position;
            PickPatrolTarget();
        }

        static Transform Player => Camera.main ? Camera.main.transform : null;

        void Update()
        {
            if (state == State.Dead)
            {
                UpdateDead();
                return;
            }

            movedThisFrame = false;
            Transform player = Player;
            bool playerAlive = player && !(PlayerHealth.Instance && PlayerHealth.Instance.IsDead);
            float dist = player ? FlatDistance(transform.position, player.position) : float.MaxValue;
            if (playerAlive && CanSense(player, dist)) lastSeenTime = Time.time;
            stateTimer -= Time.deltaTime;

            switch (state)
            {
                case State.Patrol:
                    if (playerAlive && Time.time - lastSeenTime < 0.1f)
                    {
                        state = State.Rage;
                        stateTimer = 1.3f;
                        PlayAnim(rageState, true);
                        break;
                    }
                    if (idleTimer > 0f)
                    {
                        idleTimer -= Time.deltaTime;
                        PlayAnim(idleState);
                        break;
                    }
                    PlayAnim(walkState);
                    if (MoveTowards(patrolTarget, walkSpeed))
                    {
                        idleTimer = Random.Range(1f, 3f);
                        PickPatrolTarget();
                    }
                    break;

                case State.Rage:
                    if (player) TurnTowards(player.position - transform.position);
                    if (stateTimer <= 0f) state = State.Chase;
                    break;

                case State.Chase:
                    if (!playerAlive || Time.time - lastSeenTime > loseInterestTime)
                    {
                        state = State.Patrol;
                        PickPatrolTarget();
                        break;
                    }
                    if (dist <= attackRange)
                    {
                        state = State.Attack;
                        stateTimer = attackCooldown;
                        attackLanded = false;
                        PlayAnim(attackState, true);
                        break;
                    }
                    PlayAnim(runState);
                    MoveTowards(player.position, runSpeed);
                    break;

                case State.Attack:
                    if (player) TurnTowards(player.position - transform.position);
                    if (!attackLanded && stateTimer <= attackCooldown - attackHitDelay)
                    {
                        attackLanded = true;
                        if (playerAlive && dist <= attackRange + 0.5f && PlayerHealth.Instance)
                            PlayerHealth.Instance.TakeDamage(attackDamage);
                    }
                    if (stateTimer <= 0f) state = State.Chase;
                    break;

                case State.Hit:
                    // El Animator de la demo enlaza "hit" con "death": si ya terminó el golpe, sigue persiguiendo.
                    if (stateTimer <= 0f || !IsPlaying(hitState)) state = State.Chase;
                    break;

                case State.GettingUp:
                    if (stateTimer <= 0f)
                    {
                        // Se levanta furioso y va a por ti.
                        state = State.Rage;
                        stateTimer = 1.3f;
                        PlayAnim(rageState, true);
                    }
                    break;
            }

            // Aplica la gravedad aunque esté quieto.
            if (!movedThisFrame && controller.enabled) controller.SimpleMove(Vector3.zero);
        }

        /// <summary>
        /// Tumbado en el suelo: congela la última pose de la muerte (el Animator de la demo
        /// intentaría pasar a otras animaciones) y, pasado reviveTime, se levanta.
        /// </summary>
        void UpdateDead()
        {
            stateTimer -= Time.deltaTime;

            // Se deja un momento para que el Animator empiece la caída antes de vigilarla.
            if (animator && animator.speed > 0f && stateTimer < reviveTime - 0.3f)
            {
                int deathHash = Animator.StringToHash(deathState);
                bool inTransition = animator.IsInTransition(0);
                AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
                bool enteringDeath = inTransition && animator.GetNextAnimatorStateInfo(0).shortNameHash == deathHash;
                bool stillFalling = !inTransition && current.shortNameHash == deathHash && current.normalizedTime < 0.97f;

                // Ha terminado de caer (o el Animator intenta pasar a otra animación): congela la pose final.
                if (!enteringDeath && !stillFalling)
                {
                    animator.Play(deathHash, 0, 1f);
                    animator.Update(0f);
                    animator.speed = 0f;
                }
            }

            if (stateTimer > 0f) return;

            // Se levanta: la mezcla larga hacia "idle" hace que pase de tumbado a de pie.
            if (animator) animator.speed = 1f;
            health = maxHealth;
            controller.enabled = true;
            state = State.GettingUp;
            stateTimer = getUpTime;
            if (animator && animator.HasState(0, Animator.StringToHash(idleState)))
                animator.CrossFadeInFixedTime(idleState, getUpTime, 0);
            currentAnim = idleState;
            path.Clear();
        }

        bool IsPlaying(string stateName)
        {
            if (!animator) return false;
            int hash = Animator.StringToHash(stateName);
            if (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).shortNameHash == hash) return true;
            return animator.GetCurrentAnimatorStateInfo(0).shortNameHash == hash;
        }

        bool CanSense(Transform player, float dist)
        {
            if (dist <= hearingRange) return true;
            if (dist > detectionRange) return false;

            Vector3 eye = transform.position + Vector3.up * (controller.height * 0.85f);
            Vector3 flatDir = player.position - eye;
            flatDir.y = 0f;
            // Mientras patrulla solo ve hacia delante; persiguiendo te sigue con la mirada.
            if (state == State.Patrol && Vector3.Angle(transform.forward, flatDir) > 70f) return false;

            if (Physics.Linecast(eye, player.position, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
                return hit.transform.IsChildOf(player.root);
            return true;
        }

        /// <summary>Camina hacia 'target' siguiendo las puertas. Devuelve true al llegar.</summary>
        bool MoveTowards(Vector3 target, float speed)
        {
            movedThisFrame = true;
            repathTimer -= Time.deltaTime;
            if (repathTimer <= 0f || path.Count == 0)
            {
                path.Clear();
                if (house) path.AddRange(house.FindPath(transform.position, target));
                else path.Add(target);
                pathIndex = 0;
                repathTimer = 0.4f;
            }

            Vector3 to = path[pathIndex] - transform.position;
            to.y = 0f;
            if (to.magnitude < 0.35f)
            {
                if (pathIndex >= path.Count - 1)
                {
                    controller.SimpleMove(Vector3.zero);
                    return true;
                }
                pathIndex++;
                to = path[pathIndex] - transform.position;
                to.y = 0f;
            }

            Vector3 dir = to.normalized;

            // Si lleva un segundo casi sin moverse, está atascado (un mueble, una esquina...).
            stuckTimer += Time.deltaTime;
            if (stuckTimer > 1f)
            {
                if ((transform.position - lastPosition).sqrMagnitude < 0.04f)
                {
                    if (state == State.Patrol) PickPatrolTarget();
                    nudgeTimer = 0.6f;
                    nudgeDir = Vector3.Cross(Vector3.up, dir) * (Random.value < 0.5f ? 1f : -1f);
                }
                stuckTimer = 0f;
                lastPosition = transform.position;
            }
            if (nudgeTimer > 0f)
            {
                nudgeTimer -= Time.deltaTime;
                dir = (dir + nudgeDir).normalized;
            }

            TurnTowards(dir);
            controller.SimpleMove(dir * speed);
            return false;
        }

        void TurnTowards(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            Quaternion target = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
        }

        void PickPatrolTarget()
        {
            if (!house || house.rooms.Count == 0)
            {
                Vector2 r = Random.insideUnitCircle * 3f;
                patrolTarget = transform.position + new Vector3(r.x, 0f, r.y);
                return;
            }
            int current = house.FindRoom(transform.position);
            int room = current >= 0 && Random.value < 0.5f ? current : Random.Range(0, house.rooms.Count);
            patrolTarget = house.RandomPointInRoom(room, 1.0f);
            path.Clear();
        }

        void PlayAnim(string stateName, bool restart = false)
        {
            if (!animator || string.IsNullOrEmpty(stateName)) return;
            int hash = Animator.StringToHash(stateName);
            if (!animator.HasState(0, hash)) return;

            if (!restart && currentAnim == stateName)
            {
                if (animator.IsInTransition(0)) return;
                AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
                // Si la animación no está en bucle (o el Animator saltó a otro estado), la repetimos.
                if (info.shortNameHash != hash) animator.CrossFadeInFixedTime(hash, 0.2f, 0);
                else if (!info.loop && info.normalizedTime >= 0.95f) animator.Play(hash, 0, 0f);
                return;
            }

            if (restart && currentAnim == stateName) animator.Play(hash, 0, 0f);
            else animator.CrossFadeInFixedTime(hash, 0.2f, 0);
            currentAnim = stateName;
        }

        public void TakeDamage(int amount, Vector3 hitPoint)
        {
            if (state == State.Dead) return;
            health -= amount;
            lastSeenTime = Time.time; // al recibir un disparo, va a por ti
            if (health <= 0)
            {
                state = State.Dead;
                stateTimer = reviveTime;
                if (animator) animator.speed = 1f;
                PlayAnim(deathState, true);
                controller.enabled = false;
                return;
            }
            state = State.Hit;
            stateTimer = 0.6f;
            PlayAnim(hitState, true);
        }

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
