// Home page script: the "Get the weather via gRPC" form and the Protobuf hex decoder

// ---------- Weather form ----------
const weatherForm = document.getElementById("weather-form");
const latitudeInput = document.getElementById("latitude");
const longitudeInput = document.getElementById("longitude");
const weatherResult = document.getElementById("weather-result");

// place buttons fill in the coordinates and fetch straight away
document.querySelectorAll(".chip[data-lat]").forEach((chip) => {
    chip.addEventListener("click", () => {
        latitudeInput.value = chip.dataset.lat;
        longitudeInput.value = chip.dataset.lon;
        weatherForm.requestSubmit();
    });
});

weatherForm.addEventListener("submit", async (e) => {
    // prevent the default form submission
    e.preventDefault();
    const button = weatherForm.querySelector("button[type=submit]");
    button.disabled = true;
    weatherResult.textContent = "Calling the server via gRPC...";

    try {
        // call the client's API endpoint, which calls the server via gRPC
        const url = `/weather?latitude=${encodeURIComponent(latitudeInput.value)}&longitude=${encodeURIComponent(longitudeInput.value)}`;
        const res = await fetch(url);
        if (!res.ok) {
            throw new Error(`${res.status} ${res.statusText}. If the server was asleep, wake it with /ping (see above) and try again.`);
        }
        const json = await res.json();

        // print out the result
        weatherResult.textContent = JSON.stringify(json, null, 2);

        // copy the raw protobuf into the decoder below, ready to decode
        if (json.raw_protobuf) {
            document.getElementById("hex-input").value = json.raw_protobuf;
        }
    } catch (err) {
        weatherResult.textContent = "Error: " + err.message;
    } finally {
        button.disabled = false;
    }
});

// ---------- Protobuf hex decoder ----------

// convert the hex string to a Uint8Array of bytes
function hexToBytes(hex) {
    hex = hex.replace(/[^0-9a-fA-F]/g, "");
    if (hex.length % 2 !== 0) {
        throw new Error("Hex length must be even");
    }
    const bytes = new Uint8Array(hex.length / 2);
    for (let i = 0; i < hex.length; i += 2) {
        bytes[i / 2] = parseInt(hex.substr(i, 2), 16);
    }
    return bytes;
}

// Inline .proto schema matching the WeatherResponse used in the C# API
const protoSource = `syntax = "proto3";
    message WeatherResponse {
        double temperature_2m = 1;
        int32 weather_code = 2;
        string weather_description = 3;
    }`;

// Decode the hex input to a Protobuf message and display the JSON representation
document.getElementById("protobuf-form").addEventListener("submit", (e) => {
    // prevent the default form submission
    e.preventDefault();
    const hex = document.getElementById("hex-input").value.trim();
    const output = document.getElementById("decoded");
    output.textContent = "Decoding...";

    try {
        const root = protobuf.parse(protoSource).root;
        const WeatherResponse = root.lookupType("WeatherResponse");

        const message = WeatherResponse.decode(hexToBytes(hex));
        const obj = WeatherResponse.toObject(message, {
            longs: String,
            enums: String,
            bytes: String
        });

        output.textContent = JSON.stringify(obj, null, 2);
    } catch (err) {
        output.textContent = "Error: " + err.message;
    }
});
