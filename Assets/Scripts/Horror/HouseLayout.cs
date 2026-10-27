using System.Collections.Generic;
using UnityEngine;

namespace SgiVr.Horror
{
    /// <summary>
    /// Mapa sencillo de la casa: habitaciones (rectángulos vistos desde arriba) y las puertas que las unen.
    /// Lo rellena el generador de escena y lo usan los enemigos para ir de una habitación a otra
    /// pasando por las puertas (sustituye a un NavMesh).
    /// </summary>
    public class HouseLayout : MonoBehaviour
    {
        [System.Serializable]
        public class Room
        {
            public string name;
            [Tooltip("Rectángulo en metros: x = X del mundo, y = Z del mundo.")]
            public Rect area;
        }

        [System.Serializable]
        public class Door
        {
            public int roomA;
            public int roomB;
            public Vector3 position;
            [Tooltip("Dirección (unitaria) para cruzar la puerta desde la habitación A hacia la B.")]
            public Vector3 axis;
        }

        public List<Room> rooms = new List<Room>();
        public List<Door> doors = new List<Door>();

        static HouseLayout instance;

        public static HouseLayout Find()
        {
            if (!instance) instance = FindAnyObjectByType<HouseLayout>();
            return instance;
        }

        public int FindRoom(Vector3 worldPos)
        {
            var p = new Vector2(worldPos.x, worldPos.z);
            for (int i = 0; i < rooms.Count; i++)
                if (rooms[i].area.Contains(p)) return i;
            return -1;
        }

        public Vector3 RandomPointInRoom(int room, float margin)
        {
            Rect r = rooms[room].area;
            float mx = Mathf.Min(margin, r.width * 0.4f);
            float mz = Mathf.Min(margin, r.height * 0.4f);
            return new Vector3(Random.Range(r.xMin + mx, r.xMax - mx), 0f, Random.Range(r.yMin + mz, r.yMax - mz));
        }

        /// <summary>
        /// Devuelve los puntos por los que hay que pasar para ir de 'from' a 'to',
        /// cruzando las puertas necesarias (búsqueda en anchura sobre el grafo de habitaciones).
        /// </summary>
        public List<Vector3> FindPath(Vector3 from, Vector3 to)
        {
            var path = new List<Vector3>();
            int start = FindRoom(from);
            int goal = FindRoom(to);
            if (start < 0 || goal < 0 || start == goal)
            {
                path.Add(to);
                return path;
            }

            var cameFromDoor = new int[rooms.Count];
            var visited = new bool[rooms.Count];
            for (int i = 0; i < cameFromDoor.Length; i++) cameFromDoor[i] = -1;

            var queue = new Queue<int>();
            queue.Enqueue(start);
            visited[start] = true;
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (current == goal) break;
                for (int d = 0; d < doors.Count; d++)
                {
                    int next = Other(doors[d], current);
                    if (next < 0 || visited[next]) continue;
                    visited[next] = true;
                    cameFromDoor[next] = d;
                    queue.Enqueue(next);
                }
            }

            if (!visited[goal])
            {
                path.Add(to);
                return path;
            }

            // Reconstruye la lista de puertas desde la meta hacia atrás.
            var doorSequence = new List<int>();
            for (int room = goal; room != start;)
            {
                int d = cameFromDoor[room];
                doorSequence.Add(d);
                room = Other(doors[d], room);
            }
            doorSequence.Reverse();

            int at = start;
            Vector3 flatFrom = new Vector3(from.x, 0f, from.z);
            for (int i = 0; i < doorSequence.Count; i++)
            {
                Door door = doors[doorSequence[i]];
                Vector3 dir = door.roomA == at ? door.axis : -door.axis;
                Vector3 before = door.position - dir * 0.7f;
                Vector3 after = door.position + dir * 0.7f;
                // Si ya estamos junto a la primera puerta, no hace falta volver al punto de "antes".
                if (i > 0 || Vector3.Distance(flatFrom, door.position) > 1.0f) path.Add(before);
                path.Add(after);
                at = Other(door, at);
            }
            path.Add(to);
            return path;
        }

        static int Other(Door door, int room)
        {
            if (door.roomA == room) return door.roomB;
            if (door.roomB == room) return door.roomA;
            return -1;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
            foreach (var room in rooms)
            {
                var c = new Vector3(room.area.center.x, 0.05f, room.area.center.y);
                Gizmos.DrawWireCube(c, new Vector3(room.area.width, 0.1f, room.area.height));
            }
            Gizmos.color = Color.yellow;
            foreach (var door in doors)
            {
                Gizmos.DrawSphere(door.position + Vector3.up * 0.1f, 0.2f);
                Gizmos.DrawLine(door.position - door.axis, door.position + door.axis);
            }
        }
    }
}
