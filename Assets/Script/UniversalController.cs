using UnityEngine;
using UnityEngine.UI;
using CnControls;
using System.Collections;
using System.Collections.Generic;

public class UniversalController : MonoBehaviour {

	public float speed = 3f;
	public float runSpeed = 0.7f;
    /// <summary>
    /// это позиция в которую боту необходимо двигаться
    /// </summary>
	public Vector3 nextPosition = Vector3.zero;
	public Vector3 center = Vector3.zero;
	public Rigidbody myRigidbody = null;

	public Animator HorseAnimator = null;
	//названия анимаций в аниматоре
	public string idle = "";
	public string walk = "";
	public string run = "";
    public bool asRun = false;
    public bool buttonDown = false;

    //нав меш агент
    public UnityEngine.AI.NavMeshAgent myAgent = null;
    public Transform spawnPoint = null;
	public bool runon = false;

	//энергия игрока
	public float energyMax = 100f;
	public float currentEnergy  = 0;
	public Text timer = null;
	public Image energy = null;
	public float walkGandicap = 3f;
	public float runGandicap = 7f;

	//для удаления ботов расположенных далеко от игрока
	public GameObject player = null;
    public static UniversalController playerInstance = null;
    public List<GameObject> lostBots = new List<GameObject>();                          //устаревшие боты теперь для оптимизации не удаляются с карты а заносятся в это список для дальнейшей перестановки

	//время ожидания на месте
	public float minIdleTime = 1f;
	public float maxIdleTime = 3f;

	//указываем минимальную и максимальную дистанции  на которых от центра будет получена случайная точка
	public float minDistToCenter = 1f;
	public float maxDistToCenter = 10f;

	public float stopDistance = 1f;
	public float speedRotation = 3f;

	//таймер
	public float timeInSecondsP;
	public int secondsP;
	public int minutsP; 
    
	///список всех ботов вокруг игрока
	public List<GameObject> allBots = new List<GameObject> ();

    /// <summary>
    /// Принятие решений осуществляется ботами в этой машине состояний
    /// player - этим юнитом управляет игрок
    /// selectAction - здесь случайно выбирается какой теперь действие бедет выполнять бот
    /// idle - состояние ожидания на месте
    /// moveToPoint - перемещение к точке(полученной от места спавна случаным образом но ограниченной радиусом)
    /// </summary>
	public horseState state;
	public enum horseState
	{
		player,
		selectAction,
		idle,
		moveToPoint,
	}

	/// <summary>
    /// реализация спавна(так-же выполнена в виде машины состояний)
    /// </summary>
	public spawnerState spawnState;
	public enum spawnerState{
		sleep,
		selectAction,
		spawn,
	}

	//Решил что систему спавна и удаления ботов я буду выполнять из игрока 

    /// <summary>
    /// Для удобности настройки спавна ботов попытался разместить все настройки в сериализованном классе
    /// </summary>
	[System.Serializable]
	public class spawnBotSettings{
		//дистанция на которой будут спавниться боты
		public float spawnDistance;
		//дистанция находясь на которой от игрока боты будут удалены
		public float removeDistance = 100f;
		//задаем количество ботов
		public int minCountBot = 3;
		public int maxCountBot = 100;
		//периодические спавны
		public float mintimeSpawnPause = 0f;
		public float maxtimeSpawnPause = 1f;
		//префаб ботов
		public GameObject botPrefab = null;
        //дистанция на которой впереди спавнятся боты
        public float forwardSpawnDistance = 0f;
	}

	public spawnBotSettings spawnSettings;

	void Awake(){
        //в случае если этим ботом управляет игрок
		if(state == horseState.player){
			center = this.gameObject.transform.position;
			//spawnState = spawnerState.selectAction;
			speed = 0.1f;
			currentEnergy = energyMax;
            playerInstance = this;
		}
    }

	void Update(){
        center = UniversalController.playerInstance.transform.position;
        switch (state) {
		case horseState.player:
			if (currentEnergy > 0) {
				playerMove ();
			} else {
				HorseAnimator.Play (idle ,0);
			}
			playerRotate ();
			//проверка и удаление отдаленных ботов 
			removedLostBots ();
			spawner ();
			//восстановление енергии
			restoreEnergy ();
			Timer ();
			//енергия
			energy.fillAmount = currentEnergy / energyMax;
			//обрабатываю удержание кнопки
			OnGUIRunButton();
			break;
		case horseState.selectAction:
			//принятие решения или ждем или идем
			int randomAction = (int)Random.Range (0, 2);
			switch (randomAction) {
			case 0:
				//решаем ждать
				float randomTime = (float)Random.Range (minIdleTime, maxIdleTime);
				state = horseState.idle;
				Invoke ("endIdle", randomTime);
				break;
			case 1:
			case 2:
				//решаем идти в случайную точку
				int typeX = 0;
				int typeZ = 0;
				if (getRandomBool ()) {
					typeX = 1;
				} else {
					typeX = -1;
				}
				if (getRandomBool ()) {
					typeZ = 1;
				} else {
					typeZ = -1;
				}
				//получаем новую точку
				nextPosition = getRandomPoint (typeX, typeZ);
                        //Debug.Log("nextPosition = "+ nextPosition);
				//переходим к перемещению в новую точку
				state = horseState.moveToPoint;
				break;
			}
			break;
		case horseState.moveToPoint:
                //если есть куда двигаться двигаемся
                if (nextPosition != Vector3.zero) {
                    //MoveToPoint();
				    if (Vector3.Distance (myRigidbody.transform.position, nextPosition) > UniversalController.playerInstance.stopDistance) {
				    } else {
					    nextPosition = Vector3.zero;
				    }
                    
                } else {
                    HorseAnimator.Play(idle);
				    state = horseState.selectAction;
			}
			break;
		}

        if (state != horseState.player) {
            navMeshMove();
        }

    }

    /// <summary>
    /// Метод выполняющий перемещение ботов по навмешу 
    /// </summary>
    void navMeshMove()
    {
        if (nextPosition != Vector3.zero)
        {
            //Debug.Log("nextPosition = " + nextPosition);
            myAgent.SetDestination(nextPosition);
            HorseAnimator.Play(walk, 0);
        }
        else {
            //state = horseState.selectAction;
            //myAgent.Stop();
        }
    }

    //Таймер

    /// <summary>
    /// Таймер:
    /// устанавливает необходимое значение в текстовое поле таймера
    /// </summary>
	public void Timer(){
		timeInSecondsP -= Time.deltaTime;
		secondsP = (int)(timeInSecondsP % 60);
		minutsP = (int)(timeInSecondsP / 60);
		timer.text = string.Format("{0:00}:{1:00}", minutsP, secondsP);
	}

    //восстановление энергии
    
    /// <summary>
    /// Метод восстанавливает энергию затраченную на выполнение бега
    /// </summary>
	void restoreEnergy(){
		if (currentEnergy < 100) {
			currentEnergy += 3f * Time.deltaTime;
		}
        if (currentEnergy < 3) {
            asRun = true;
            HorseAnimator.Play(idle);
            Invoke("endAsRun", 7);
        }
	}
	//метод который выполняет спавн ботов

    /// <summary>
    /// Метод спавнит ботов на определенном расстоянии от игрока
    /// теперь этот метод перемещает ботов на новую позицию если они устарели
    /// </summary>
	void spawner(){
        if (lostBots.Count > 0)
        {
            switch (spawnState)
            {
                case spawnerState.spawn:
                    //предварительные настройки
                    int typeX = 0;
                    int typeZ = 0;
                    if (getRandomBool())
                    {
                        typeX = 1;
                    }
                    else {
                        typeX = -1;
                    }
                    if (getRandomBool())
                    {
                        typeZ = 1;
                    }
                    else {
                        typeZ = -1;
                    }
                    //значит имеет смысл спавнить ботов
                    center = UniversalController.playerInstance.gameObject.transform.position;                                            //обновляем нашу позицию
                    for (int i = 0; i < lostBots.Count; i++)
                    {
                        GameObject iters = lostBots[i];
                        spawnBot(getRandomPoint(typeX, typeZ), iters);
                    }
                    //spawnState = spawnerState.selectAction;
                    break;
                    /*
                //выбор случайного действия
                case spawnerState.selectAction:
                    int randomAction = (int)Random.Range(0, 2);
                    switch (randomAction)
                    {
                        case 0:
                            spawnState = spawnerState.spawn;
                            break;
                        case 1:
                        case 2:
                            float randomtime = (float)Random.Range(spawnSettings.mintimeSpawnPause, spawnSettings.maxtimeSpawnPause);
                            Invoke("endSleep", randomtime);
                            spawnState = spawnerState.sleep;
                            break;
                    }
                    break;
                case spawnerState.sleep:
                    break;
                    */
            }
        }
	}

    /// <summary>
    /// Метод прерывающий невозможность бега(накопилась энергия)
    /// </summary>
    void endAsRun() {
        asRun = false;
    }

	//метод спавнит бота в указанной точке
    /// <summary>
    /// теперь этот метод будет работать иначе, он будет брать устаревшего бота и переставлять его на новое место
    /// </summary>
    /// <param name="spawnPosition">
    /// Место в которое нужно переместить этого юнита
    /// </param>
    /// <param name="player">
    /// ссылка на бота
    /// </param>
    /// <returns></returns>
	public GameObject spawnBot(Vector3 spawnPosition, GameObject player){
        Debug.Log("Да тута я тут");
        GameObject cloneBot = null;
        //cloneBot = (GameObject)Instantiate(spawnSettings.botPrefab, spawnPosition, Quaternion.identity);
        cloneBot = player;
        lostBots.Remove(cloneBot);
        //cloneBot.GetComponent<NavMeshAgent>().Stop();
        //Надо их впереди создавать немного:
        //Vector3 spawnComplete = UniversalController.playerInstance.transform.position + (UniversalController.playerInstance.transform.forward * UniversalController.playerInstance.spawnSettings.forwardSpawnDistance);
        cloneBot.transform.position = UniversalController.playerInstance.spawnPoint.position;
        Debug.Log("spawnPosition = "+ spawnPosition);
        UniversalController botController = cloneBot.GetComponent<UniversalController>();
        botController.center = player.transform.position;
        botController.state = horseState.selectAction;
        return cloneBot;
	}

	//обрываем через инвок
	void endSleep(){
		//spawnState = spawnerState.selectAction;
	}

	//слабое место но для быстрого варианта сойдет
    /// <summary>
    /// Так как мало ботов в целом то ничего тут особо страшного нет этож не foreach скомпилинный юнькой
    /// </summary>
	public void removedLostBots(){
		for(int i = 0; i < allBots.Count; i++){
			GameObject bot = allBots [i];
            float distanceToPlayer = Vector3.Distance(UniversalController.playerInstance.myRigidbody.transform.position, bot.transform.position);
            //Debug.Log("distanceToPlayer = "+ distanceToPlayer);
            if (distanceToPlayer > UniversalController.playerInstance.spawnSettings.removeDistance)
            {
                bot.GetComponent<UniversalController>().destroyBot(bot);
                Debug.Log("Контроллер");
            }
		}
	}

    /// <summary>
    /// Метод уничтожает ботов
    /// </summary>
	void destroyBot(GameObject objects){
        if (!UniversalController.playerInstance.lostBots.Contains(objects)) {
            UniversalController.playerInstance.lostBots.Add(objects);
            Debug.Log("добавил я его в список отсталых");
        }
		//Destroy (this.gameObject, 3f);
	}

    /// <summary>
    /// Мтод выполняет перемещение:
    /// </summary>
	void playerMove(){
		//если нажата стрелка
		if(Input.GetAxis("Vertical")!= 0 & runon == false){
            //this.transform.position += transform.forward * speed * Input.GetAxis ("Vertical");
            Vector3 direction = transform.forward * speed * Input.GetAxis("Vertical");
            this.myRigidbody.AddForce(direction*1000);
			AnimatorStateInfo stateInfo = HorseAnimator.GetCurrentAnimatorStateInfo(0);
			if(!stateInfo.IsName(walk)){
				HorseAnimator.Play (walk ,0);
			}
			//currentEnergy -= walkGandicap * Time.deltaTime;
		}

		//если нажат джойстик
		if( CnInputManager.GetAxis("Vertical")!=0 & Input.GetAxis("Vertical")==0 & runon == false){
            //this.transform.position += transform.forward * speed * CnInputManager.GetAxis("Vertical");
            Vector3 direction = transform.forward * speed * CnInputManager.GetAxis("Vertical");
            this.myRigidbody.AddForce(direction * 1000);
            AnimatorStateInfo stateInfo = HorseAnimator.GetCurrentAnimatorStateInfo(0);
			if(!stateInfo.IsName(walk)){
				HorseAnimator.Play (walk ,0);
			}
			//currentEnergy -= walkGandicap * Time.deltaTime;
		}

	
		//если нажата стрелка и шифт
		if((Input.GetKey(KeyCode.LeftShift) & (Input.GetAxis("Vertical")!=0 & !buttonDown & !asRun) || (CnInputManager.GetAxis("Vertical")!=0) & buttonDown & runon == true & !asRun)){
            //this.transform.position += transform.forward * runSpeed * Input.GetAxis ("Vertical");
            Vector3 direction = Vector3.zero;
            if (Input.GetAxis("Vertical")!=0) {
                direction = transform.forward * runSpeed * Input.GetAxis("Vertical");
            }
            if (CnInputManager.GetAxis("Vertical")!= 0)
            {
                direction = transform.forward * runSpeed * CnInputManager.GetAxis("Vertical");
            }
            myRigidbody.AddForce(direction*800);
            AnimatorStateInfo stateInfo = HorseAnimator.GetCurrentAnimatorStateInfo(0);
			if (!stateInfo.IsName (run)) {
				HorseAnimator.Play (run, 0);
			}
			currentEnergy -= runGandicap * Time.deltaTime;
		}

		if(Input.GetAxis("Vertical")==0 & CnInputManager.GetAxis("Vertical")==0){
			HorseAnimator.Play (idle ,0);
		}
	}
    
    /// <summary>
    /// Метод выполняет поворот персонажа игрока
    /// </summary>
	void playerRotate(){
		if (Input.GetAxis ("Horizontal") != 0) {
			this.transform.RotateAround (this.transform.position, transform.up, Input.GetAxis("Horizontal"));
		}
		if(CnInputManager.GetAxis("Horizontal")!=0){
			this.transform.RotateAround (this.transform.position, transform.up, CnInputManager.GetAxis("Horizontal"));
		}
	}

    /// <summary>
    /// Метод выполняет перемещение и поворот к цели
    /// </summary>
	void MoveToPoint(){
        navMeshMove();
        //movementForward ();
        //rotate ();
    }

	//Метод перемещения вперед(просто пинаю бота вперед и при этом рулю его поворотом)
	void movementForward(){
		myRigidbody.transform.position += myRigidbody.transform.forward  * speed * Time.deltaTime;
		HorseAnimator.Play (walk ,0);
	}

	//метод вращения к позиции
	void rotate(){
		if(this.nextPosition != Vector3.zero){
			this.myRigidbody.transform.rotation = Quaternion.Slerp(this.myRigidbody.transform.rotation, Quaternion.LookRotation(nextPosition - this.myRigidbody.transform.position), speedRotation * Time.deltaTime);
		}
	}

    /// <summary>
    /// Метод используется в инвоке для прерывания состояния ожидания на месте
    /// </summary>
	void endIdle(){
		state = horseState.selectAction;
	}

    //получаем случайную точку вокруг нашего центра
    /// <summary>
    /// Этот метод возвращает случайно собранный Вектор3
    /// вектор возможен в любом из четырех квадратов вокруг указанной центральной точки
    /// </summary>
    /// <param name="typeX">
    /// координата x центра квадрата спавна
    /// </param>
    /// <param name="typeZ">
    /// координата z центра квадрата спавна
    /// </param>
    /// <returns></returns>
    Vector3 getRandomPoint(int typeX, int typeZ){
		Vector3 newPosition = Vector3.zero;
		newPosition = new Vector3 (center.x + (GetRandomFloat(minDistToCenter, maxDistToCenter) * typeX), 0, center.z + (GetRandomFloat(minDistToCenter, maxDistToCenter)) * typeZ);
		return newPosition;
	}
		
	//не совсем рандом
	float GetRandomFloat(float min, float max){
		float returned = (float)Random.Range (min, max);
		return returned;
	}

	//функция нужна чтобы охватить все четыре четверти вокруг центра
	bool getRandomBool(){
		bool randomBool = false;
		int randomization = (int)Random.Range (0,2);
		switch(randomization){
		case 0:
			randomBool = true;
			break;
		case 1:
		case 2:
			randomBool = false;
			break;
		}
		return randomBool;
	}

	public void OnGUIRunButton(){
        if (Input.GetKey(KeyCode.LeftShift)) {
            runon = true;
        }
        if (!Input.GetKey(KeyCode.LeftShift) & !buttonDown) {
            runon = false;
        }
	}

	//требуется правильно обрабатывать кнопку бежать
	public void OnRun(){
		runon = true;
        buttonDown = true;
	}
	public void OfRun(){
        buttonDown = false;
		runon = false;
	}

}
