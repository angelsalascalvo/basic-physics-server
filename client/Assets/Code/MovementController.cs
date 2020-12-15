﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;

/**
 * FUNCIONALIDAD DEL SCRIPT
 */
public class MovementController : MonoBehaviour{
    
    private struct Input {
        public int tick;
        public byte direction;
    }

    //// REF PUB
    public ConnectServer connectServer;
    public ValidatePhysic validatePhysic;
    // 🎲 Simulacion latencia
    [Header("Latency Simulate")]
    public Slider sliderLatency;
    public Text textLatency;
    // 🎲 Simulacion perdida de datagramas
    [Header("Lost Datagram Simulate")]
    public Slider sliderLostDatagram;
    public Text textLostDatagram;



    //// VAR PRI
    private Rigidbody2D rb;
    private EnumDirection direction = EnumDirection.None;

    private float timer;
    private int tick = 0;
    private short limitsInputs = 50;

    //---------------------------------------------------------------

    /**
     * INICIALIZACION
     */
    void Start(){
        rb = GetComponent<Rigidbody2D>();
    }

    //---------------------------------------------------------------

    void Update() {
        timer += Time.deltaTime;

        while (timer >= Time.fixedDeltaTime) {
            timer -= Time.fixedDeltaTime;
            Debug.LogWarning("paso");
            if (connectServer.isConnected()) {
                byte[] data = new byte[508];
                BinaryWriter bw = new BinaryWriter(new MemoryStream(data));

                bw.Write((byte)2); //ActionCode 2.
                validatePhysic.saveInputsBuffer(tick, (byte)direction);
                switch (direction) {
                    case EnumDirection.Left:
                        rb.velocity = new Vector2(-3f, rb.velocity.y);
                        break;
                    case EnumDirection.Right:
                        rb.velocity = new Vector2(3f, rb.velocity.y);
                        break;
                    case EnumDirection.Jump:
                        Debug.LogWarning("Jump");
                        rb.AddForce(new Vector2(0, 60f));
                        break;
                    case EnumDirection.None:
                        rb.velocity = new Vector2(0, rb.velocity.y);
                        break;
                }
                
                Debug.Log("=============== TICK " + tick + " ===============");
                Debug.Log("ultimo tick del servidor:" + connectServer.getLastTickServer());

                //Agregar el listado de ticks sin recibir por parte del servidor
                List<Input> listInput = createListInputToServer();

                int countInputs = Mathf.Min((int)listInput.Count, limitsInputs); //Obtener el numero de inputs a enviar con un maximo de {limitsInputs}
                bw.Write(countInputs); //
                Debug.Log("Input concatenados" + countInputs);
                //Obtener los n ultimos resultamos del listado para evitar desbordar el array de bytes del datagrama 
                //con una cantidad demasiado grande de inputs
                for (int i = Mathf.Max(0, listInput.Count - limitsInputs); i < listInput.Count; ++i) {
                    bw.Write(listInput[i].tick);
                    bw.Write(listInput[i].direction);
                    Debug.Log("agregado tick" + listInput[i].tick + " direccion " + listInput[i].direction);
                }

                Debug.Log("==========================================\n");


                Physics2D.Simulate(Time.fixedDeltaTime); //¿Es realmente necesario simular fisica en cliente? Si no se simula podemos migrar código a Fixed update

                //🎲 Simulacion perdida datagramas: Probabilidad de que el datagrama se pierda
                if (StaticMethods.percent((short)sliderLostDatagram.value)) {
                    Debug.LogWarning("Paquete perdido");
                } else {
                    //🎲 Simulacion de latencia
                    StartCoroutine(sendToServerSimulateLatency(data));
                }

                validatePhysic.savePositionBuffer(tick, transform.position);
                tick++;
            }
        }

        //🎲 Simulacion de latencia
        textLatency.text = "Latencia de envío: " + (int)sliderLatency.value + "ms";
        // 🎲 Simulacion perdida de datagramas
        textLostDatagram.text = "Perdida envío datagramas: " + (int)sliderLostDatagram.value + "%";
    }
    private void FixedUpdate() {
        Debug.LogWarning("pasoFixed");
    }
    //---------------------------------------------------------------

    /**
     * Comprueba cuales son los tick no recibidos por el servidor y elabora un listado con los datos de los mismos
     */
    private List<Input> createListInputToServer(){
        List<Input> listInput = new List<Input>();
        //Comprobar cuales son los tick aun no recibidos por el servidor para volver a enviarselo 
        //por si se ha producido alguna perdida de alguno de ellos.
        for (int i = connectServer.getLastTickServer(); i<=tick; i++) {
            Input input;
            input.tick = i;
            input.direction = validatePhysic.readInputsBuffer(i);
            listInput.Add(input);
        }
        return listInput;
    }

    //---------------------------------------------------------------

    /**
     * 🎲 Simulacion de latencia
     * Corrutina que espera X segundos antes de enviar los datos al servidor 
     */
    IEnumerator sendToServerSimulateLatency(byte[] data) {
        yield return new WaitForSeconds(sliderLatency.value/1000);
        connectServer.sendDataToServer(data);
    }


    //---------------------------------------------------------------

    public void setMovement(EnumDirection direction) {
        this.direction = direction;
    }
   

}
