
#include <DHT.h>

#define SOIL_PIN A0
#define WATER_PIN A1
#define DHT_PIN 2
#define RELAY_PIN 7
#define DHT_TYPE DHT22

DHT dht(DHT_PIN, DHT_TYPE);

const int SOIL_DRY_THRESHOLD = 796;
const int WATER_MIN_LEVEL = 200;

const unsigned long PUMP_DURATION = 2000;
const unsigned long WATERING_INTERVAL = 60000;
const unsigned long SENSOR_INTERVAL = 2000;

unsigned long lastSensorRead = 0;
unsigned long lastWatering = 0;
unsigned long pumpStartTime = 0;

bool pumpRunning = false;
bool wateringDone = false;
bool automaticMode = true;

String commandBuffer = "";

int soilValue = 0;
int waterValue = 0;
float temperature = NAN;
float humidity = NAN;

void stopPump() {
  digitalWrite(RELAY_PIN, LOW);
  pumpRunning = false;
}

void startPump() {
  if (waterValue <= WATER_MIN_LEVEL) {
    stopPump();
    return;
  }

  digitalWrite(RELAY_PIN, HIGH);
  pumpRunning = true;
  pumpStartTime = millis();
}

void readSensors() {
  soilValue = analogRead(SOIL_PIN);
  waterValue = analogRead(WATER_PIN);
  temperature = dht.readTemperature();
  humidity = dht.readHumidity();
}

void processCommand(String command) {
  command.trim();
  command.toUpperCase();

  if (command == "AUTO") {
    stopPump();
    automaticMode = true;
    lastWatering = millis();
    wateringDone = true;
  }
  else if (command == "MANUAL ON") {
    automaticMode = false;
    readSensors();
    startPump();
  }
  else if (command == "MANUAL OFF") {
    automaticMode = false;
    stopPump();
  }
}

void readCommands() {
  while (Serial.available() > 0) {
    char c = Serial.read();

    if (c == '\n') {
      processCommand(commandBuffer);
      commandBuffer = "";
    }
    else if (c != '\r') {
      if (commandBuffer.length() < 40) {
        commandBuffer += c;
      }
      else {
        commandBuffer = "";
      }
    }
  }
}

void sendData() {
  bool lowWater = waterValue <= WATER_MIN_LEVEL;
  bool sensorError = isnan(temperature) || isnan(humidity);

  Serial.print("SOIL:");
  Serial.print(soilValue);

  Serial.print(";WATER:");
  Serial.print(waterValue);

  Serial.print(";TEMP:");
  if (sensorError) Serial.print("---");
  else Serial.print(temperature, 1);

  Serial.print(";HUM:");
  if (sensorError) Serial.print("---");
  else Serial.print(humidity, 1);

  Serial.print(";PUMP:");
  Serial.print(pumpRunning ? "ON" : "OFF");

  Serial.print(";MODE:");
  Serial.print(automaticMode ? "AUTO" : "MANUAL");

  Serial.print(";ERROR:");
  if (sensorError) Serial.print("SENSOR");
  else if (lowWater) Serial.print("LOW_WATER");
  else Serial.print("NONE");

  Serial.print(";STATUS:");
  if (sensorError || lowWater) {
    Serial.println("ERROR");
  }
  else if (automaticMode && wateringDone && !pumpRunning) {
    Serial.println("WAITING");
  }
  else {
    Serial.println("OK");
  }
}

void setup() {
  digitalWrite(RELAY_PIN, LOW);
  pinMode(RELAY_PIN, OUTPUT);

  Serial.begin(9600);
  dht.begin();

  readSensors();
}

void loop() {
  unsigned long now = millis();

  readCommands();

  // Ограничаваме всяко включване на помпата до 2 секунди.
  if (pumpRunning && now - pumpStartTime >= PUMP_DURATION) {
    stopPump();
  }

  if (now - lastSensorRead >= SENSOR_INTERVAL) {
    lastSensorRead = now;
    readSensors();

    // Спираме помпата при ниско ниво на водата.
    if (waterValue <= WATER_MIN_LEVEL) {
      stopPump();
    }

    // Автоматично поливане.
    if (automaticMode &&
        !pumpRunning &&
        soilValue > SOIL_DRY_THRESHOLD &&
        waterValue > WATER_MIN_LEVEL &&
        (!wateringDone ||
         now - lastWatering >= WATERING_INTERVAL)) {

      startPump();
      lastWatering = now;
      wateringDone = true;
    }

    sendData();
  }
}
