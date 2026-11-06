using System;

namespace SevenDays.Procedural
{
    public enum ZombieState { Patrol, Chase, Attack, Stunned, Dead }

    /// <summary>
    /// Máquina de estados pura del zombie: patrulla -> persigue -> ataca. Usa histéresis (pierde al jugador a 1.4x del rango de detección) para que no
    /// parpadee entre estados en el límite, y un pequeño margen para dejar de atacar.
    /// </summary>
    public class ZombieStateMachine
    {
        public ZombieState State { get; private set; } = ZombieState.Patrol;
        public float AggroRange = 18f, AttackRange = 1.5f, LoseRangeFactor = 1.4f;
        public float StateTime { get; private set; }

        public void Reset() { State = ZombieState.Patrol; StateTime = 0f; }

        /// <param name="distance">distancia al jugador</param>
        /// <param name="canSee">hay línea de visión (o el jugador acaba de disparar cerca)</param>
        /// <param name="stunned">aturdido ahora mismo</param>
        /// <param name="dead">ya murió</param>
        public ZombieState Tick(float dt, float distance, bool canSee, bool stunned, bool dead, bool playerDead = false)
        {
            StateTime += dt;
            ZombieState next = State;
            if (dead) next = ZombieState.Dead;
            else if (stunned) next = ZombieState.Stunned;
            else if (playerDead) next = ZombieState.Patrol;
            else
            {
                switch (State)
                {
                    case ZombieState.Patrol:
                        if (distance <= AggroRange && canSee) next = ZombieState.Chase;
                        break;
                    case ZombieState.Chase:
                        if (distance <= AttackRange) next = ZombieState.Attack;
                        else if (distance > AggroRange * LoseRangeFactor) next = ZombieState.Patrol;
                        break;
                    case ZombieState.Attack:
                        if (distance > AttackRange * 1.25f) next = ZombieState.Chase;
                        break;
                    default: // saliendo de aturdimiento o muerte: vuelve a decidir según la distancia
                        next = distance <= AttackRange ? ZombieState.Attack : (distance <= AggroRange * LoseRangeFactor ? ZombieState.Chase : ZombieState.Patrol);
                        break;
                }
            }
            if (next != State) { State = next; StateTime = 0f; }
            return State;
        }
    }
}
