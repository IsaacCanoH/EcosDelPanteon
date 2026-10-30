const fs=require('fs'),path=require('path'),cp=require('child_process');
const backup='Backups/RestauracionNivel1';
const manifest=JSON.parse(fs.readFileSync(path.join(backup,'guid-map.json'),'utf8'));
const failures=[];
const check=(ok,message)=>{if(!ok)failures.push(message);};
const reverse=new Map(Object.entries(manifest.mappings).map(([a,b])=>[b,a]));
const undo=text=>text.replace(/guid: (\w{32})/g,(whole,id)=>reverse.has(id)?'guid: '+reverse.get(id):whole);
const normalize=text=>text.replace(/\r\n/g,'\n');
for(const file of manifest.restored){
    const original=cp.execFileSync('git',['show','HEAD:'+file],{encoding:'utf8',maxBuffer:32*1024*1024});
    check(normalize(fs.readFileSync(file,'utf8'))===normalize(original),'Archivo original alterado: '+file);
}
for(const file of manifest.level2Files){
    const after=fs.readFileSync(file,'utf8');
    // El mapa del nivel 2 puede editarse; sus referencias deben seguir aisladas.
    if(file.replaceAll('\\','/')!=='Assets/Scenes/Cementerio_Nivel2.unity'){
        const before=fs.readFileSync(path.join(backup,file),'utf8');
        check(before===undo(after),'Cambió contenido de un prefab del nivel 2: '+file);
    }
    check(![...after.matchAll(/guid: (\w{32})/g)].some(m=>Object.hasOwn(manifest.mappings,m[1])),'Referencia compartida antigua en '+file);
}
for(const file of manifest.copied){
    const portable=file.replaceAll('\\','/');
    const original=portable.startsWith('Assets/Prefabs/Level2')
        ?portable.replace('Assets/Prefabs/Level2','Assets/Cainos/Pixel Art Top Down - Basic/Prefab')
        :'Assets/Fonts/PressStart2P-Regular SDF.asset';
    const saved=path.join(backup,original);
    const before=fs.readFileSync(fs.existsSync(saved)?saved:original,'utf8');
    check(before===undo(fs.readFileSync(file,'utf8')),'Copia no equivalente: '+file);
}
const lines=[`Archivos originales comprobados contra HEAD: ${manifest.restored.length}.`,`Prefabs independientes del nivel 2: ${manifest.copied.length-1}.`,'El mapa del nivel 2 se rediseñó. Sus referencias siguen aisladas; los prefabs de HUD, jugador, juguetes y enemigo conservan su contenido.',...failures.map(f=>'ERROR: '+f)];
if(!failures.length)lines.push('OK: restauración del nivel 1 y aislamiento del nivel 2 verificados.');
fs.writeFileSync('Documentation/Nivel1-restauracion.txt',lines.join('\n')+'\n');
console.log(lines.join('\n'));
if(failures.length)process.exitCode=1;
