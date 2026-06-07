#include <WiFi.h>
#include <WebSocketsServer.h>
#include <FastLED.h>

// ── CONFIG — edit these for your setup ────────────────────────────────────────
const char* SSID     = "dsv-extrality-lab";
const char* PASSWORD = "expiring-unstuck-slider";

#define LED_PIN     4        // GPIO pin connected to DATA line of the strip
#define NUM_LEDS    144       // total LEDs on your strip
#define LED_TYPE    WS2812B
#define COLOR_ORDER GRB
#define BRIGHTNESS  80       // 0-255; keep <=100 for long strips on USB power

#define WS_PORT     8082     // Unity will connect here (different from button ESP32's 8081)
#define BLINK_MS    250      // ms per blink state — 250 ms = 2 Hz, matches Unity canvas
// ─────────────────────────────────────────────────────────────────────────────

CRGB leds[NUM_LEDS];

// Colours matching Unity DualRageBarUI exactly
const CRGB C_BLUE    = CRGB(0,  0,  255); // P1  #185FA5
const CRGB C_GREEN   = CRGB(0,  255, 0);  // P2  #3B6D11
const CRGB C_RED     = CRGB(255, 0,  0);  // overlap / victory  #E24B4A
const CRGB C_RED_DIM = CRGB(0,  0,  0);  // dim state for blink
const CRGB C_OFF     = CRGB(0,   0,   0);

float rageP1  = 0.0f;
float rageP2  = 0.0f;
float maxRage = 100.0f;

bool          blinkOn    = true;
unsigned long lastBlink  = 0;
unsigned long lastUpdate = 0;

WebSocketsServer wsServer(WS_PORT);

// ─────────────────────────────────────────────────────────────────────────────

void setup() {
  Serial.begin(115200);

  FastLED.addLeds<LED_TYPE, LED_PIN, COLOR_ORDER>(leds, NUM_LEDS);
  FastLED.setBrightness(BRIGHTNESS);
  fill_solid(leds, NUM_LEDS, C_OFF);
  FastLED.show();

  // Blink first LED white while connecting
  WiFi.begin(SSID, PASSWORD);
  Serial.print("[WiFi] Connecting");
  while (WiFi.status() != WL_CONNECTED) {
    leds[0] = (millis() / 400) % 2 == 0 ? CRGB::White : C_OFF;
    FastLED.show();
    delay(400);
    Serial.print(".");
  }
  fill_solid(leds, NUM_LEDS, C_OFF);
  FastLED.show();

  Serial.println();
  Serial.print("[WiFi] Connected — IP: ");
  Serial.println(WiFi.localIP());

  wsServer.begin();
  wsServer.onEvent(onWsEvent);
  Serial.printf("[WS] Server listening on port %d\n", WS_PORT);
}

void loop() {
  wsServer.loop();
  
  // Serial test input
  if (Serial.available()) {
    String input = Serial.readStringUntil('\n');
    input.trim();
    parseMessage(input);
  }

  unsigned long now = millis();
  if (now - lastBlink >= BLINK_MS) {
    lastBlink = now;
    blinkOn   = !blinkOn;
  }
  if (now - lastUpdate >= 33) {
    lastUpdate = now;
    updateLEDs();
  }
}

// ─────────────────────────────────────────────────────────────────────────────

void onWsEvent(uint8_t client, WStype_t type, uint8_t* payload, size_t length) {
  switch (type) {
    case WStype_CONNECTED:
      Serial.printf("[WS] Client %u connected\n", client);
      break;
    case WStype_DISCONNECTED:
      Serial.printf("[WS] Client %u disconnected\n", client);
      break;
    case WStype_TEXT:
      parseMessage(String((char*)payload));
      break;
    default:
      break;
  }
}

// Expects:  "rage:P1,P2,MAX"  e.g.  "rage:45.2,67.8,100.0"
void parseMessage(const String& msg) {
  if (!msg.startsWith("rage:")) return;

  String data = msg.substring(5);
  int c1 = data.indexOf(',');
  int c2 = data.lastIndexOf(',');
  if (c1 < 0 || c2 <= c1) return;

  rageP1  = data.substring(0, c1).toFloat();
  rageP2  = data.substring(c1 + 1, c2).toFloat();
  maxRage = data.substring(c2 + 1).toFloat();
  if (maxRage < 1.0f) maxRage = 100.0f;
}

// ─────────────────────────────────────────────────────────────────────────────
// LED layout — mirrors DualRageBarUI exactly:
//
//  LED 0 ──────────────────────────────────────────────── LED N-1
//  [===BLUE (P1)===][=RED(blink)=][  off  ][=RED(blink)=][===GREEN (P2)===]
//   grows left→right   overlap                overlap      grows right←left
//
// When P1+P2 fills ≥ 100%: entire strip turns red and blinks (victory).
// ─────────────────────────────────────────────────────────────────────────────
void updateLEDs() {
  float p1 = constrain(rageP1 / maxRage, 0.0f, 1.0f);
  float p2 = constrain(rageP2 / maxRage, 0.0f, 1.0f);

  int p1n    = (int)(p1 * NUM_LEDS);
  int p2n    = (int)(p2 * NUM_LEDS);
  int combined = p1n + p2n;

  // Victory: combined fills the whole bar
  if (combined >= NUM_LEDS) {
    fill_solid(leds, NUM_LEDS, blinkOn ? C_RED : C_RED_DIM);
    FastLED.show();
    return;
  }

  int redN   = max(0, combined - NUM_LEDS);
  int blueN  = max(0, p1n - redN);
  int greenN = max(0, p2n - redN);

  CRGB redNow = blinkOn ? C_RED : C_RED_DIM;

  for (int i = 0; i < NUM_LEDS; i++) {
    if      (i < blueN)                   leds[i] = C_BLUE;   // P1
    else if (i < blueN + redN)            leds[i] = redNow;   // overlap blink
    else if (i >= NUM_LEDS - greenN)      leds[i] = C_GREEN;  // P2
    else                                  leds[i] = C_OFF;    // empty
  }

  FastLED.show();
}