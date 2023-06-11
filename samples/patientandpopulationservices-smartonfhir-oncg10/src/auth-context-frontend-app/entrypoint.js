"use-strict;"
/* eslint-disable @typescript-eslint/no-var-requires */
const dotenv = require("dotenv");
const fs = require("fs");
const os = require("os");
const path = require('path');


let envFilePath = ""
let configRoot = "ENV_CONFIG"
let outputFile = "./public/env-config.js"

for (let i = 2; i < process.argv.length; i++) {
    switch (process.argv[i]) {
        case "-e":
            envFilePath = process.argv[++i]
        break;
        case "-o":
            outputFile = process.argv[++i]
        break;
        case "-c":
            configRoot = process.argv[++i]
        break;
        default:
            throw Error(`unknown option ${process.argv[i]}`)
    }
}

if (envFilePath === "" || !fs.existsSync(envFilePath))
{
    let azdFilePath = path.join(__dirname, '..', '..', '.azure', 'config.json');

    console.log(`Attempting to load azd config file path from '${azdFilePath}'`)
    
    const data = fs.readFileSync(azdFilePath, 'utf8');
    const json = JSON.parse(data);

    console.log(`Using azd default environment of ${json.defaultEnvironment}`);

    // Extract the element you're interested in
    envFilePath = path.join(__dirname, '..', '..', '.azure', json.defaultEnvironment, '.env');
}

if (fs.existsSync(envFilePath)) {
    console.log(`Loading environment file from '${envFilePath}'`)

    dotenv.config({
        path: envFilePath
    })    
}
else
{
    console.log(`Could not find .env file at '${envFilePath}'`)
}

console.log(`Generating JS configuration output to: ${outputFile}`)
console.log(`Current directory is: ${process.cwd()}`)

fs.writeFileSync(outputFile, `window.${configRoot} = {${os.EOL}${
    Object.keys(process.env).filter(x => x.startsWith("REACT_APP_")).map(key => {
        console.log(`- Found '${key}'`);
        return `${key}: '${process.env[key]}',${os.EOL}`;
    }).join("")
}${os.EOL}}`);